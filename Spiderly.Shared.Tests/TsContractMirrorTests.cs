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
    /// <para>
    /// One mirror here is TS → template, not C# ↔ TS: every <c>OPERATOR_WORDS</c> key in the filter store needs
    /// a value in the init template's English Transloco file.
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
        /// Requires a value in the init template's English Transloco file for every <c>OPERATOR_WORDS</c> key.
        /// Why: the comment on <c>OPERATOR_WORDS</c> in <c>filters/filter-store.ts</c>, and
        /// <c>.claude/rules/library-template-text.md</c>. English is the only locale the template seeds.
        /// </summary>
        [Fact]
        public void Every_filter_operator_key_has_a_value_in_the_init_template_English_translations()
        {
            List<string> missing = OperatorKeysWithoutEnglishValue(
                RepoFile("Angular/projects/spiderly/src/lib/filters/filter-store.ts"),
                NetAndAngularFilesGenerator.GetTranslocoEnJsonCode());

            Assert.True(missing.Count == 0,
                $"These OPERATOR_WORDS keys (filter-store.ts) have no value in the init template's English " +
                $"Transloco file: {string.Join(", ", missing)}. Seed each one in " +
                $"NetAndAngularFilesGenerator.GetTranslocoEnJsonCode().");
        }

        [Fact]
        public void NegativeControl_an_operator_key_without_an_English_value_is_reported()
        {
            const string ts = """
                const OPERATOR_WORDS: Record<FilterValueKind, OperatorWords> = {
                  text: {
                    [MatchModeCodes.Equals]: { pickerKey: 'Equals', phraseKey: 'FilterChipEquals' },
                    [MatchModeCodes.In]: { pickerKey: 'In', phraseKey: 'FilterChipIn' },
                  },
                };
                """;
            const string en = """{ "Equals": "Equals", "FilterChipEquals": "is", "FilterChipIn": " " }""";

            Assert.Equal(new List<string> { "In", "FilterChipIn" }, OperatorKeysWithoutEnglishValue(ts, en));
        }

        /// <summary>
        /// The <c>OPERATOR_WORDS</c> keys that have no string value, or a blank one, in <paramref name="enJson"/>.
        /// </summary>
        private static List<string> OperatorKeysWithoutEnglishValue(string filterStoreTs, string enJson)
        {
            using JsonDocument en = JsonDocument.Parse(enJson);

            return OperatorWordKeys(filterStoreTs)
                .Where(key => !en.RootElement.TryGetProperty(key, out JsonElement value)
                    || value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(value.GetString()))
                .ToList();
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
