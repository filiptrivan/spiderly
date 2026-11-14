using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Tests;

/// <summary>
/// Every runtime <c>@angular/*</c> in <c>Angular/package.json</c> is pinned exact, at the version the
/// <c>spiderly init</c> template emits, so the lock regenerates the same way on every install. A caret or a
/// tilde floats to a newer patch on a fresh install, and that install dies on an ERESOLVE peer conflict
/// with the pinned compiler.
/// </summary>
public class AngularPinTests
{
    [Fact]
    public void Library_pins_every_runtime_angular_package_at_the_init_template_version()
    {
        string library = RepoFile("Angular/package.json");

        Assert.NotEmpty(RuntimeAngularPins(library));
        Assert.Empty(PinViolations(library, TemplatePackageJson()));
    }

    [Theory]
    [InlineData("^19.2.13")]
    [InlineData("~19.2.13")]
    [InlineData("19.2.x")]
    [InlineData("0.0.1")]
    public void A_floating_pin_or_a_version_the_init_template_does_not_emit_is_refused(string corePin)
    {
        JsonNode library = JsonNode.Parse(RepoFile("Angular/package.json"))!;
        library["dependencies"]!["@angular/core"] = corePin;

        string violation = Assert.Single(PinViolations(library.ToJsonString(), TemplatePackageJson()));
        Assert.StartsWith("@angular/core ", violation);
    }

    [Fact]
    public void A_runtime_angular_package_the_init_template_does_not_emit_is_refused()
    {
        JsonNode library = JsonNode.Parse(RepoFile("Angular/package.json"))!;
        library["dependencies"]!["@angular/cdk"] = "19.2.13";

        string violation = Assert.Single(PinViolations(library.ToJsonString(), TemplatePackageJson()));
        Assert.StartsWith("@angular/cdk ", violation);
    }

    private static readonly Regex ExactVersion = new(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$");

    private static List<string> PinViolations(string libraryPackageJson, string templatePackageJson)
    {
        Dictionary<string, string> emitted = RuntimeAngularPins(templatePackageJson);
        List<string> violations = new();

        foreach ((string name, string pin) in RuntimeAngularPins(libraryPackageJson))
        {
            if (!ExactVersion.IsMatch(pin))
                violations.Add($"{name} is \"{pin}\", a range. Pin it exact, at the version NetAndAngularFilesGenerator.GetPackageJsonData emits.");
            else if (!emitted.TryGetValue(name, out string? templatePin))
                violations.Add($"{name} is not in the init template. Add it to NetAndAngularFilesGenerator.GetPackageJsonData at {pin}.");
            else if (pin != templatePin)
                violations.Add($"{name} is {pin}, and the init template emits {templatePin}. Pin one version in both, then regenerate Angular/package-lock.json.");
        }

        return violations;
    }

    private static Dictionary<string, string> RuntimeAngularPins(string packageJson) =>
        JsonNode.Parse(packageJson)!["dependencies"]!.AsObject()
            .Where(dependency => dependency.Key.StartsWith("@angular/", StringComparison.Ordinal))
            .ToDictionary(dependency => dependency.Key, dependency => dependency.Value!.GetValue<string>(), StringComparer.Ordinal);

    private static string TemplatePackageJson() =>
        NetAndAngularFilesGenerator.GetPackageJsonData("App", "0.0.0", isRunningFromNuget: true);

    private static string RepoFile(string relativePath)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Spiderly.sln")))
            dir = dir.Parent;

        if (dir is null)
            throw new DirectoryNotFoundException($"Could not locate the repo root (Spiderly.sln) above {AppContext.BaseDirectory}.");

        return File.ReadAllText(Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
