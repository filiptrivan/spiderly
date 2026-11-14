using System.Text.Json;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Guards the package.json that <c>spiderly init</c> writes for a new app's Frontend against the Angular
    /// library's own package.json. A new app lacks a library peer only when the template keeps its own list of
    /// them, and a package manager that installs no peers (yarn, or the <c>--dev</c> mode with no spiderly
    /// package) then fails at the first import of that peer.
    /// </summary>
    public class InitTemplatePackageJsonTests
    {
        private const string LibraryPackageJson = "Angular/projects/spiderly/package.json";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Every_library_peer_dependency_is_a_dependency_of_the_new_app_with_the_library_range(bool isRunningFromNuget)
        {
            Dictionary<string, string> peers = Section(RepoFiles.Read(LibraryPackageJson), "peerDependencies");
            Dictionary<string, string> dependencies = Section(
                NetAndAngularFilesGenerator.GetPackageJsonData("TestApp", "1.2.3", isRunningFromNuget), "dependencies");

            List<string> wrong = peers
                .Where(peer => !dependencies.TryGetValue(peer.Key, out string? spec) || spec != peer.Value)
                .Select(peer => $"{peer.Key}: {peer.Value} in {LibraryPackageJson}, " +
                    $"{(dependencies.TryGetValue(peer.Key, out string? spec) ? spec : "nothing")} in the new app")
                .ToList();

            Assert.True(wrong.Count == 0,
                $"The new app's package.json must carry each library peer with its range: {string.Join("; ", wrong)}.");
        }

        /// <summary>
        /// <c>@angular/compiler-cli</c> needs the exact <c>@angular/compiler</c> of its own version. A toolchain
        /// pinned apart from the Angular range makes npm refuse the install (ERESOLVE).
        /// </summary>
        [Theory]
        [InlineData("@angular-devkit/build-angular")]
        [InlineData("@angular/cli")]
        [InlineData("@angular/compiler-cli")]
        public void The_Angular_toolchain_takes_the_library_Angular_range(string toolchainPackage)
        {
            string angularRange = Section(RepoFiles.Read(LibraryPackageJson), "peerDependencies")["@angular/core"];
            Dictionary<string, string> devDependencies = Section(
                NetAndAngularFilesGenerator.GetPackageJsonData("TestApp", "1.2.3", isRunningFromNuget: true), "devDependencies");

            Assert.Equal(angularRange, devDependencies[toolchainPackage]);
        }

        private static Dictionary<string, string> Section(string packageJson, string section)
        {
            using JsonDocument package = JsonDocument.Parse(packageJson);

            return package.RootElement.GetProperty(section).EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        }
    }
}
