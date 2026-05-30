using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Spiderly.Security.SecurityControllers;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.Enums;

namespace Spiderly.MetadataExporter;

/// <summary>
/// Internal build tool. Reflects the public Spiderly contracts + reads their
/// compiler-emitted XML doc summaries and emits framework-metadata.json — the
/// single source of truth the skill reference docs (and later the spiderly-website
/// docs) are generated from. Run in CI with a git-diff guard so the JSON can never
/// drift from the code. Fails loudly (non-zero exit) on any extraction problem.
/// </summary>
internal static class Program
{
    // Const-string-class contracts: the wire VALUE is the contract (clients switch on it).
    private static readonly Type[] ConstStringClasses = { typeof(ApiErrorCodes), typeof(MatchModeCodes) };

    // True C# enums: the member NAME is the contract (referenced via nameof(...)).
    // Extracted from Spiderly.Shared only — the SourceGenerators copy of
    // UIControlTypeCodes has extra internal values (Table, None) that are NOT a
    // public contract, so reading the Shared enum is itself the public filter.
    private static readonly Type[] Enums = { typeof(UIControlTypeCodes) };

    // Base controllers whose public action methods form an API surface that clients call.
    private static readonly Type[] Controllers = { typeof(SecurityBaseController<,,>) };

    // Collects every exported member missing an XML <summary> so a single run reports them all at once
    // (rather than aborting on the first). The run still fails before writing the JSON if any are missing.
    private static readonly List<string> MissingDocs = new();

    private static int Main(string[] args)
    {
        try
        {
            string outPath = GetOption(args, "--out") ?? "framework-metadata.json";

            Assembly sharedAssembly = typeof(ApiErrorCodes).Assembly;
            Assembly securityAssembly = typeof(SecurityBaseController<,,>).Assembly;
            Dictionary<string, string> docs = LoadXmlDocSummaries(sharedAssembly, securityAssembly);

            var enums = new List<EnumModel>();
            foreach (Type t in ConstStringClasses)
                enums.Add(BuildConstStringClass(t, docs));
            foreach (Type t in Enums)
                enums.Add(BuildEnum(t, docs));

            var controllers = new List<ControllerModel>();
            foreach (Type t in Controllers)
                controllers.Add(BuildController(t, docs));

            List<AttributeModel> attributes = BuildAttributes(sharedAssembly, docs);

            if (MissingDocs.Count > 0)
                throw new InvalidOperationException(
                    $"{MissingDocs.Count} exported member(s) are missing an XML <summary> — every exported member must be " +
                    $"documented (its summary becomes the generated reference docs). Add a /// <summary> to each:" +
                    Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", MissingDocs));

            var metadata = new Metadata(enums, controllers, attributes);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                // Keep human-readable chars (apostrophes, <, >) literal in the doc summaries.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            // Normalise to LF + trailing newline so the committed artifact is byte-identical
            // whether regenerated on Windows (dev) or Linux (CI) — the git-diff guard depends on it.
            string json = JsonSerializer.Serialize(metadata, options).Replace("\r\n", "\n") + "\n";
            File.WriteAllText(outPath, json);

            int memberCount = enums.Sum(m => m.Members.Count);
            int endpointCount = controllers.Sum(c => c.Endpoints.Count);
            Console.WriteLine($"Wrote {Path.GetFullPath(outPath)} — {enums.Count} enums ({memberCount} members), {controllers.Count} controllers ({endpointCount} endpoints), {attributes.Count} attributes.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: framework-metadata extraction failed: {ex.Message}");
            return 1;
        }
    }

    private static EnumModel BuildConstStringClass(Type type, Dictionary<string, string> docs)
    {
        List<MemberModel> members = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .OrderBy(f => f.Name, StringComparer.Ordinal) // deterministic — reflection order is not guaranteed
            .Select(f => new MemberModel(
                f.Name,
                (string)f.GetRawConstantValue()!,
                RequireSummary(docs, $"F:{type.FullName}.{f.Name}", $"{type.Name}.{f.Name}")))
            .ToList();

        if (members.Count == 0)
            throw new InvalidOperationException($"{type.Name}: no public const string members found — unexpected type shape.");

        return new EnumModel(type.Name, "constStringClass", type.Namespace!, GetClassSummary(docs, type), members);
    }

    private static EnumModel BuildEnum(Type type, Dictionary<string, string> docs)
    {
        if (!type.IsEnum)
            throw new InvalidOperationException($"{type.Name} is registered as an enum but is not a C# enum.");

        List<MemberModel> members = Enum.GetNames(type)
            .Select(n => (Name: n, Value: Convert.ToInt64(Enum.Parse(type, n))))
            .OrderBy(x => x.Value) // declaration order (by underlying value), stable
            .Select(x => new MemberModel(
                x.Name,
                null, // the enum contract is the name, not its ordinal
                RequireSummary(docs, $"F:{type.FullName}.{x.Name}", $"{type.Name}.{x.Name}")))
            .ToList();

        return new EnumModel(type.Name, "enum", type.Namespace!, GetClassSummary(docs, type), members);
    }

    private static ControllerModel BuildController(Type type, Dictionary<string, string> docs)
    {
        var httpVerbs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HttpGetAttribute"] = "GET",
            ["HttpPostAttribute"] = "POST",
            ["HttpPutAttribute"] = "PUT",
            ["HttpDeleteAttribute"] = "DELETE",
            ["HttpPatchAttribute"] = "PATCH",
        };

        List<EndpointModel> endpoints = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => (Method: m, Attrs: m.GetCustomAttributesData()))
            .Where(x => x.Attrs.Any(a => httpVerbs.ContainsKey(a.AttributeType.Name)))
            .OrderBy(x => x.Method.Name, StringComparer.Ordinal) // deterministic — reflection order is not guaranteed
            .Select(x =>
            {
                string verbKey = x.Attrs.Select(a => a.AttributeType.Name).First(httpVerbs.ContainsKey);
                bool auth = x.Attrs.Any(a => a.AttributeType.Name == "AuthGuardAttribute");
                string summary = RequireMethodSummary(docs, type, x.Method.Name, $"{ControllerName(type)}.{x.Method.Name}");
                return new EndpointModel(x.Method.Name, httpVerbs[verbKey], auth, summary);
            })
            .ToList();

        if (endpoints.Count == 0)
            throw new InvalidOperationException($"{ControllerName(type)}: no HTTP action methods found — unexpected controller shape.");

        return new ControllerModel(ControllerName(type), GetClassSummary(docs, type), endpoints);
    }

    private static string ControllerName(Type type) => type.Name.Split('`')[0];

    private static List<AttributeModel> BuildAttributes(Assembly assembly, Dictionary<string, string> docs)
    {
        return assembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract
                        && typeof(Attribute).IsAssignableFrom(t)
                        && t.Namespace is not null
                        && t.Namespace.StartsWith("Spiderly.Shared.Attributes", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal) // deterministic — reflection order is not guaranteed
            .Select(t =>
            {
                // Display the usage form: "UIControlWidthAttribute" -> "UIControlWidth" (used as [UIControlWidth]).
                string display = t.Name.EndsWith("Attribute", StringComparison.Ordinal)
                    ? t.Name[..^"Attribute".Length]
                    : t.Name;
                AttributeTargets targets = t.GetCustomAttribute<AttributeUsageAttribute>()?.ValidOn ?? AttributeTargets.All;
                return new AttributeModel(display, targets.ToString(), RequireSummary(docs, $"T:{t.FullName}", display));
            })
            .ToList();
    }

    private static string RequireMethodSummary(Dictionary<string, string> docs, Type type, string methodName, string display)
    {
        // Method doc IDs include the parameter signature ("M:Type.Method(p1,p2)"). These action methods are
        // not overloaded, so match by the "M:Type.Method(" prefix (or the no-arg exact id) instead of
        // reconstructing the full signature.
        string prefix = $"M:{type.FullName}.{methodName}";
        foreach (KeyValuePair<string, string> kv in docs)
            if ((kv.Key == prefix || kv.Key.StartsWith(prefix + "(", StringComparison.Ordinal)) && !string.IsNullOrWhiteSpace(kv.Value))
                return kv.Value;

        MissingDocs.Add(display);
        return string.Empty;
    }

    private static Dictionary<string, string> LoadXmlDocSummaries(params Assembly[] assemblies)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Assembly assembly in assemblies)
        {
            string xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException(
                    $"XML doc file not found at '{xmlPath}'. Ensure <GenerateDocumentationFile>true</GenerateDocumentationFile> " +
                    $"is set on {assembly.GetName().Name} and the project was built before running the exporter.");

            foreach (XElement member in XDocument.Load(xmlPath).Descendants("member"))
            {
                string? id = member.Attribute("name")?.Value;
                XElement? summary = member.Element("summary");
                if (id is null || summary is null)
                    continue;

                string cleaned = CleanXmlText(summary);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    map[id] = cleaned;
            }
        }

        return map;
    }

    /// <summary>
    /// Flattens a &lt;summary&gt; to one line: drops &lt;example&gt;/&lt;code&gt; blocks, replaces
    /// cross-reference tags (&lt;see&gt;/&lt;paramref&gt;) — which carry no inner text — with a readable
    /// token, keeps remaining prose, and collapses whitespace.
    /// </summary>
    private static string CleanXmlText(XElement element)
    {
        var clone = new XElement(element);

        // Drop <example>/<code> blocks and <br/> breaks, leaving a SPACE so neighbouring words don't fuse
        // ("...their parent" + "Example:" -> "...their parent Example:"). Re-query each iteration so a
        // <code> nested inside an <example> isn't processed after its parent is already detached.
        while (clone.Descendants().FirstOrDefault(d => d.Name.LocalName is "example" or "code" or "br") is { } block)
            block.ReplaceWith(new XText(" "));

        // <para> carries text but is a block boundary — keep the text, add a trailing space.
        foreach (XElement para in clone.Descendants().Where(d => d.Name.LocalName == "para").ToList())
            para.AddAfterSelf(new XText(" "));

        // Cross-reference tags have no inner text — substitute a readable token.
        foreach (XElement xref in clone.Descendants().Where(d => d.Name.LocalName is "see" or "seealso").ToList())
        {
            string token = xref.Attribute("cref") is { } cref ? ShortName(cref.Value)
                : xref.Attribute("langword")?.Value
                ?? xref.Attribute("href")?.Value
                ?? string.Empty;
            xref.ReplaceWith(new XText(token));
        }

        foreach (XElement pref in clone.Descendants().Where(d => d.Name.LocalName is "paramref" or "typeparamref").ToList())
            pref.ReplaceWith(new XText(pref.Attribute("name")?.Value ?? string.Empty));

        string text = Regex.Replace(clone.Value, @"\s+", " ").Trim();

        // The Spiderly attribute docs follow a "<b>Usage:</b> ... <b>Example:</b> <code>...</code>" template.
        // Those labels read fine in an IDE tooltip but are noise in a flat table cell, and the trailing
        // "Example:" is orphaned once its <code> is dropped — strip the leading Usage: and dangling Example:.
        text = Regex.Replace(text, @"^Usage:\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*Example:?\s*$", "", RegexOptions.IgnoreCase).Trim();

        return text;
    }

    /// <summary>Reduces an XML-doc cref (e.g. "P:Spiderly.Shared.DTO.ApiErrorDTO.ErrorCode") to its simple name ("ErrorCode").</summary>
    private static string ShortName(string? cref)
    {
        if (string.IsNullOrEmpty(cref))
            return string.Empty;
        string path = cref.IndexOf(':') is int colon && colon >= 0 ? cref[(colon + 1)..] : cref;
        int paren = path.IndexOf('(');
        if (paren >= 0)
            path = path[..paren]; // drop a method cref's parameter list, e.g. "...Login(System.String)" -> "...Login"
        int lastDot = path.LastIndexOf('.');
        return lastDot >= 0 ? path[(lastDot + 1)..] : path;
    }

    private static string RequireSummary(Dictionary<string, string> docs, string docId, string display)
    {
        if (docs.TryGetValue(docId, out string? summary) && !string.IsNullOrWhiteSpace(summary))
            return summary;

        MissingDocs.Add(display);
        return string.Empty;
    }

    private static string? GetClassSummary(Dictionary<string, string> docs, Type type)
        => docs.TryGetValue($"T:{type.FullName}", out string? summary) ? summary : null;

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}

internal sealed record Metadata(List<EnumModel> Enums, List<ControllerModel> Controllers, List<AttributeModel> Attributes);

internal sealed record EnumModel(string Name, string Kind, string Namespace, string? Summary, List<MemberModel> Members);

internal sealed record MemberModel(string Name, string? Value, string Summary);

internal sealed record ControllerModel(string Name, string? Summary, List<EndpointModel> Endpoints);

internal sealed record EndpointModel(string Name, string Verb, bool Auth, string Summary);

internal sealed record AttributeModel(string Name, string Target, string Summary);
