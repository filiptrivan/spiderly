using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Requires the Angular library's package.json to declare each package that its sources import.
    /// ng-packagr builds the library without the declaration. An app then gets the package only when
    /// another package brings it, at whatever version that package wants.
    /// </summary>
    public class AngularLibraryImportTests
    {
        private const string LibrarySources = "Angular/projects/spiderly/src";
        private const string LibraryPackageJson = "Angular/projects/spiderly/package.json";

        [Fact]
        public void Every_package_the_library_imports_is_declared_in_its_package_json()
        {
            List<string> sources = Directory
                .EnumerateFiles(RepoFiles.FullPath(LibrarySources), "*.ts", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".spec.ts", StringComparison.Ordinal))
                .Select(File.ReadAllText)
                .ToList();
            Assert.Contains("@angular/core", ImportedPackages(sources));

            List<string> undeclared = Undeclared(sources, Declared(RepoFiles.Read(LibraryPackageJson)));

            Assert.True(undeclared.Count == 0,
                $"The Angular library imports packages that {LibraryPackageJson} does not declare: " +
                $"{string.Join(", ", undeclared)}. Add each to its peerDependencies, and pin it in Angular/package.json.");
        }

        [Fact]
        public void NegativeControl_an_undeclared_import_is_reported()
        {
            const string ts = """
                import { Component } from '@angular/core';
                import { ButtonModule } from 'primeng/button';
                import mime from 'mime';
                import 'reflect-metadata';
                export * from '@scope/package/deep/path';
                const lazy = () => import("lazy-package");
                import { local } from './local';
                """;

            Assert.Equal(
                ["@scope/package", "lazy-package", "mime", "reflect-metadata"],
                Undeclared([ts], new HashSet<string> { "@angular/core", "primeng" }));
        }

        /// <summary>The package of each bare import in <paramref name="sources"/>, a deep import by its package.</summary>
        private static SortedSet<string> ImportedPackages(IEnumerable<string> sources) =>
            new(sources
                .SelectMany(ts => Regex.Matches(ts, @"\b(?:from|import)\s*\(?\s*['""]([^'"".\/][^'""]*)['""]"))
                .Select(match => match.Groups[1].Value)
                .Select(specifier => specifier.StartsWith('@')
                    ? string.Join('/', specifier.Split('/').Take(2))
                    : specifier.Split('/')[0]),
                StringComparer.Ordinal);

        private static List<string> Undeclared(IEnumerable<string> sources, IReadOnlySet<string> declared) =>
            ImportedPackages(sources).Where(package => !declared.Contains(package)).ToList();

        /// <summary>The packages in the peerDependencies and the dependencies of <paramref name="packageJson"/>.</summary>
        private static HashSet<string> Declared(string packageJson)
        {
            using JsonDocument package = JsonDocument.Parse(packageJson);

            return new[] { "peerDependencies", "dependencies" }
                .SelectMany(section => package.RootElement.GetProperty(section).EnumerateObject())
                .Select(entry => entry.Name)
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
