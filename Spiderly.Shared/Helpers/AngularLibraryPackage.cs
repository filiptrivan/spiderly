using System.Text.Json;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// The two package.json files of the <c>spiderly</c> Angular library, which this assembly embeds at build time
    /// (see <c>Spiderly.Shared.csproj</c>). The library's own file names its peer dependencies. Its workspace's
    /// file (<c>Angular/package.json</c>) pins the version that builds and tests the library.
    /// <c>spiderly init</c> reads both. So the init template keeps no copy of the peers or of their versions.
    /// </summary>
    internal static class AngularLibraryPackage
    {
        private const string LibraryResource = "Spiderly.AngularLibrary.package.json";
        private const string WorkspaceResource = "Spiderly.AngularWorkspace.package.json";

        private static readonly Lazy<Dictionary<string, string>> peerRanges = new(() => Section(LibraryResource, "peerDependencies"));
        private static readonly Lazy<Dictionary<string, string>> workspaceDependencies = new(() => Section(WorkspaceResource, "dependencies"));
        private static readonly Lazy<Dictionary<string, string>> workspaceDevDependencies = new(() => Section(WorkspaceResource, "devDependencies"));

        /// <summary>
        /// Each peer dependency of the library, with the version the workspace's dependencies pin for it.
        /// A peer that the workspace does not pin keeps its peer range.
        /// </summary>
        internal static Dictionary<string, string> PeerDependencyVersions() =>
            peerRanges.Value.ToDictionary(
                peer => peer.Key,
                peer => workspaceDependencies.Value.TryGetValue(peer.Key, out string? pinned) ? pinned : peer.Value,
                StringComparer.Ordinal);

        /// <summary>The version the workspace's devDependencies pin for <paramref name="packageName"/>, such as <c>@angular/cli</c>.</summary>
        internal static string WorkspaceDevDependencyVersion(string packageName) => workspaceDevDependencies.Value[packageName];

        private static Dictionary<string, string> Section(string resourceName, string section)
        {
            using Stream stream = typeof(AngularLibraryPackage).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' is missing from Spiderly.Shared.");
            using JsonDocument package = JsonDocument.Parse(stream);

            return package.RootElement.GetProperty(section).EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal);
        }
    }
}
