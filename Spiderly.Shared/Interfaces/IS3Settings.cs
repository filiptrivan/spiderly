namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the S3-compatible object storage settings. Implemented by
    /// <see cref="Settings"/> and injected into the storage services, so they depend on configuration
    /// passed in rather than the global mutable <c>SettingsProvider</c> static.
    /// </summary>
    public interface IS3Settings
    {
        /// <summary>Name of the S3 bucket blobs are stored in.</summary>
        string S3BucketName { get; }

        /// <summary>Public base URL used to build externally reachable URLs for public blobs.</summary>
        string S3PublicEndpoint { get; }
    }
}
