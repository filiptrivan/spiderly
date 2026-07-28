namespace Spiderly.Shared
{
    /// <summary>
    /// S3-compatible object storage options. Bound from the <c>AppSettings:Spiderly.Shared</c>
    /// configuration section and injected into the storage services as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    public class S3Options
    {
        /// <summary>Name of the S3 bucket blobs are stored in. Optional in config (only required when an S3 storage service is used — those throw at construction when it is missing).</summary>
        public string? S3BucketName { get; set; }

        /// <summary>Public base URL used to build externally reachable URLs for public blobs. Optional in config (required only by <c>S3PublicStorageService</c>, which throws at construction when it is missing).</summary>
        public string? S3PublicEndpoint { get; set; }
    }
}
