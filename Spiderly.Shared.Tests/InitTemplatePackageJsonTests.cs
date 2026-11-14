using System.Text.Json;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Guards the package.json that <c>spiderly init</c> writes for a new app's Frontend against the Angular
    /// library's two package.json files. A new app lacks a library peer only when the template keeps its own list of
    /// them, and a package manager that installs no peers (yarn, or the <c>--dev</c> mode with no spiderly
    /// package) then fails at the first import of that peer.
    /// </summary>
    public class InitTemplatePackageJsonTests
    {
        private const string LibraryPackageJson = "Angular/projects/spiderly/package.json";
        private const string WorkspacePackageJson = "Angular/package.json";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Every_library_peer_dependency_is_a_dependency_of_the_new_app_at_the_workspace_version(bool isRunningFromNuget)
        {
            Dictionary<string, string> peers = Section(RepoFiles.Read(LibraryPackageJson), "peerDependencies");
            Assert.NotEmpty(peers);

            List<string> wrong = WrongPeers(
                peers,
                Section(RepoFiles.Read(WorkspacePackageJson), "dependencies"),
                Section(NetAndAngularFilesGenerator.GetPackageJsonData("TestApp", "1.2.3", isRunningFromNuget), "dependencies"));

            Assert.True(wrong.Count == 0,
                $"The new app's package.json must carry each peer of {LibraryPackageJson}, at the version " +
                $"{WorkspacePackageJson} pins: {string.Join("; ", wrong)}.");
        }

        [Fact]
        public void NegativeControl_a_peer_missing_or_at_another_version_is_reported()
        {
            Assert.Equal(
                ["b: 2.0.0 expected, nothing in the new app", "c: ^3.0.0 expected, 3.1.0 in the new app"],
                WrongPeers(
                    peers: new() { ["a"] = "^1.0.0", ["b"] = "^2.0.0", ["c"] = "^3.0.0" },
                    workspace: new() { ["a"] = "1.0.0", ["b"] = "2.0.0" },
                    dependencies: new() { ["a"] = "1.0.0", ["c"] = "3.1.0" }));
        }

        /// <summary>
        /// <c>@angular/compiler-cli</c> needs the exact <c>@angular/compiler</c> of its own version. A toolchain
        /// pinned apart from the Angular the peers bring makes npm refuse the install (ERESOLVE).
        /// </summary>
        [Theory]
        [InlineData("@angular-devkit/build-angular")]
        [InlineData("@angular/cli")]
        [InlineData("@angular/compiler-cli")]
        public void The_Angular_toolchain_takes_the_workspace_version(string toolchainPackage)
        {
            Dictionary<string, string> workspace = Section(RepoFiles.Read(WorkspacePackageJson), "devDependencies");
            Dictionary<string, string> devDependencies = Section(
                NetAndAngularFilesGenerator.GetPackageJsonData("TestApp", "1.2.3", isRunningFromNuget: true), "devDependencies");

            Assert.Equal(workspace[toolchainPackage], devDependencies[toolchainPackage]);
        }

        /// <summary>
        /// Each peer that <paramref name="dependencies"/> lacks, or holds at another version than the one
        /// <paramref name="workspace"/> pins. A peer the workspace does not pin is expected at its peer range.
        /// </summary>
        private static List<string> WrongPeers(
            Dictionary<string, string> peers, Dictionary<string, string> workspace, Dictionary<string, string> dependencies) =>
            peers
                .Select(peer => (Name: peer.Key, Expected: workspace.GetValueOrDefault(peer.Key, peer.Value)))
                .Where(peer => dependencies.GetValueOrDefault(peer.Name) != peer.Expected)
                .Select(peer => $"{peer.Name}: {peer.Expected} expected, {dependencies.GetValueOrDefault(peer.Name) ?? "nothing"} in the new app")
                .ToList();

        private static Dictionary<string, string> Section(string packageJson, string section)
        {
            using JsonDocument package = JsonDocument.Parse(packageJson);

            return package.RootElement.GetProperty(section).EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        }
    }
}
