using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Guards the C# ↔ TypeScript mirrors of cross-language string contracts. <c>ApiErrorCodes</c> and
    /// <c>MatchModeCodes</c> are hand-maintained in both languages (the Angular admin and storefronts switch
    /// on the wire values); a value added/renamed on one side and not the other drifts silently. These tests
    /// compare the member sets order-independently so the drift fails CI.
    /// <para>
    /// The TS copies are <em>guarded</em> rather than <em>generated</em> on purpose: generating them from the
    /// (alphabetically-sorted, for deterministic extraction) framework metadata would reorder the enums away
    /// from their readable declaration order, for no functional gain on a string contract.
    /// </para>
    /// </summary>
    public class TsContractMirrorTests
    {
        [Fact]
        public void ApiErrorCodes_TypeScript_mirror_matches_CSharp()
        {
            Dictionary<string, string> csharp = ConstStringMembers(typeof(ApiErrorCodes));
            Dictionary<string, string> ts = ParseTsStringMap(
                RepoFile("Angular/projects/spiderly/src/lib/errors/api-error-codes.ts"));

            Assert.Equal(csharp, ts);
        }

        [Fact]
        public void MatchModeCodes_TypeScript_mirror_matches_CSharp()
        {
            Dictionary<string, string> csharp = ConstStringMembers(typeof(MatchModeCodes));
            Dictionary<string, string> ts = ParseTsStringMap(
                RepoFile("Angular/projects/spiderly/src/lib/enums/match-mode-enum-codes.ts"));

            Assert.Equal(csharp, ts);
        }

        /// <summary>
        /// The filter bar's operator picker and chip show the VALUE of a key in <c>OPERATOR_WORDS</c>
        /// (<c>filters/filter-store.ts</c>), never the key. A key with no value renders as the raw key, and a
        /// consumer that matches an option by its label (an e2e test, conditional logic) breaks silently. The init
        /// template is the only place these keys reach a consumer, because a consumer's key extraction scans only
        /// its own <c>src/</c>. English is the only locale the template seeds.
        /// </summary>
        [Fact]
        public void Every_filter_operator_key_has_a_value_in_the_init_template_English_translations()
        {
            List<string> keys = OperatorWordKeys(
                RepoFile("Angular/projects/spiderly/src/lib/filters/filter-store.ts"));
            using JsonDocument en = JsonDocument.Parse(NetAndAngularFilesGenerator.GetTranslocoEnJsonCode());

            List<string> missing = keys
                .Where(key => !en.RootElement.TryGetProperty(key, out JsonElement value)
                    || value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(value.GetString()))
                .ToList();

            Assert.True(missing.Count == 0,
                $"These OPERATOR_WORDS keys (filter-store.ts) have no value in the init template's English " +
                $"Transloco file: {string.Join(", ", missing)}. Seed each one in " +
                $"NetAndAngularFilesGenerator.GetTranslocoEnJsonCode().");
        }

        /// <summary>
        /// The distinct <c>pickerKey</c> and <c>phraseKey</c> values inside the <c>OPERATOR_WORDS</c> object literal.
        /// </summary>
        private static List<string> OperatorWordKeys(string ts)
        {
            Match table = Regex.Match(ts, @"const OPERATOR_WORDS\b[\s\S]*?\n\};");
            Assert.True(table.Success, "filter-store.ts declares no `const OPERATOR_WORDS ... };` block.");

            List<string> keys = Regex.Matches(table.Value, @"(?:pickerKey|phraseKey)\s*:\s*[""']([^""']+)[""']")
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Assert.True(keys.Count > 0, "No pickerKey or phraseKey found in OPERATOR_WORDS (filter-store.ts).");

            return keys;
        }

        /// <summary>Public <c>const string</c> members of a static class → name/value map.</summary>
        private static Dictionary<string, string> ConstStringMembers(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

        /// <summary>
        /// Extracts <c>Name: 'value'</c> (const object) and <c>Name = "value"</c> (enum) string members from a
        /// small TS contract file. The accompanying <c>export type ... = (typeof ...)</c> / <c>as const</c> lines
        /// have no quoted RHS, so they don't match.
        /// </summary>
        private static Dictionary<string, string> ParseTsStringMap(string ts)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(ts, @"(\w+)\s*[:=]\s*[""']([^""']+)[""']"))
                map[m.Groups[1].Value] = m.Groups[2].Value;
            return map;
        }

        private static string RepoFile(string relativePath)
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Spiderly.sln")))
                dir = dir.Parent;

            if (dir is null)
                throw new DirectoryNotFoundException(
                    $"Could not locate the repo root (Spiderly.sln) above {AppContext.BaseDirectory}.");

            string path = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Expected mirror file not found: {path}");

            return File.ReadAllText(path);
        }
    }
}
