using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.Enums;

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
    /// One mirror here is TS → JSON, not C# ↔ TS: every <c>OPERATOR_WORDS</c> key in the filter store needs
    /// a value in the library's English Transloco file, <c>i18n/en.json</c>.
    /// </para>
    /// </summary>
    public class TsContractMirrorTests
    {
        [Fact]
        public void ApiErrorCodes_TypeScript_mirror_matches_CSharp()
        {
            Dictionary<string, string> csharp = ConstStringMembers(typeof(ApiErrorCodes));
            Dictionary<string, string> ts = ParseTsStringMap(
                RepoFiles.Read("Angular/projects/spiderly/src/lib/errors/api-error-codes.ts"));

            Assert.Equal(csharp, ts);
        }

        [Fact]
        public void MatchModeCodes_TypeScript_mirror_matches_CSharp()
        {
            Dictionary<string, string> csharp = ConstStringMembers(typeof(MatchModeCodes));
            Dictionary<string, string> ts = ParseTsStringMap(
                RepoFiles.Read("Angular/projects/spiderly/src/lib/enums/match-mode-enum-codes.ts"));

            Assert.Equal(csharp, ts);
        }

        /// <summary>
        /// Requires a value in the library's English Transloco file for every <c>OPERATOR_WORDS</c> key.
        /// Why: the comment on <c>OPERATOR_WORDS</c> in <c>filters/filter-store.ts</c>. The library ships its
        /// strings in its <c>spiderly</c> scope, and English is the language it ships.
        /// </summary>
        [Fact]
        public void Every_filter_operator_key_has_a_value_in_the_library_English_translations()
        {
            List<string> missing = OperatorKeysWithoutEnglishValue(
                RepoFiles.Read("Angular/projects/spiderly/src/lib/filters/filter-store.ts"),
                RepoFiles.Read("Angular/projects/spiderly/src/lib/i18n/en.json"));

            Assert.True(missing.Count == 0,
                $"These OPERATOR_WORDS keys (filter-store.ts) have no value in the library's English " +
                $"Transloco file: {string.Join(", ", missing)}. Give each key the `{ScopePrefix}` prefix, and add " +
                $"it without the prefix to Angular/projects/spiderly/src/lib/i18n/en.json.");
        }

        [Fact]
        public void NegativeControl_an_operator_key_without_an_English_value_is_reported()
        {
            const string ts = """
                const OPERATOR_WORDS: Record<FilterValueKind, OperatorWords> = {
                  text: {
                    [MatchModeCodes.Equals]: { pickerKey: 'spiderly.Equals', phraseKey: 'spiderly.FilterChipEquals' },
                    [MatchModeCodes.In]: { pickerKey: 'spiderly.In', phraseKey: 'spiderly.FilterChipIn' },
                    [MatchModeCodes.Contains]: { pickerKey: 'Contains', phraseKey: 'spiderly.FilterChipContains' },
                  },
                };
                """;
            const string en = """
                { "Equals": "Equals", "FilterChipEquals": "is", "FilterChipIn": " ", "Contains": "Contains", "FilterChipContains": "contains" }
                """;

            Assert.Equal(
                new List<string> { "spiderly.In", "spiderly.FilterChipIn", "Contains" },
                OperatorKeysWithoutEnglishValue(ts, en));
        }

        /// <summary>The prefix of a key in the library's own Transloco scope. Its file holds the key without it.</summary>
        private const string ScopePrefix = "spiderly.";

        /// <summary>
        /// The <c>OPERATOR_WORDS</c> keys that have no string value, or a blank one, in <paramref name="enJson"/>.
        /// A key without the <see cref="ScopePrefix"/> has none, since the library reads it from the app's file.
        /// </summary>
        private static List<string> OperatorKeysWithoutEnglishValue(string filterStoreTs, string enJson)
        {
            using JsonDocument en = JsonDocument.Parse(enJson);

            return OperatorWordKeys(filterStoreTs)
                .Where(key => !key.StartsWith(ScopePrefix, StringComparison.Ordinal)
                    || !en.RootElement.TryGetProperty(key[ScopePrefix.Length..], out JsonElement value)
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
    }
}
