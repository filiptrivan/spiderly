using System.Text.Json;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// The <c>spiderly</c> Angular library's own package.json, which this assembly embeds at build time
    /// (see <c>Spiderly.Shared.csproj</c>). <c>spiderly init</c> reads it from here, so the init template
    /// keeps no copy of the library's peer dependencies.
    /// </summary>
    internal static class AngularLibraryPackage
    {
        private const string ResourceName = "Spiderly.AngularLibrary.package.json";

        /// <summary>Each peer dependency of the library, by package name, with its version range.</summary>
        internal static Dictionary<string, string> ReadPeerDependencies()
        {
            using Stream stream = typeof(AngularLibraryPackage).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing from Spiderly.Shared.");
            using JsonDocument package = JsonDocument.Parse(stream);

            return package.RootElement.GetProperty("peerDependencies").EnumerateObject()
                .ToDictionary(peer => peer.Name, peer => peer.Value.GetString()!, StringComparer.Ordinal);
        }
    }
}
