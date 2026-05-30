using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
