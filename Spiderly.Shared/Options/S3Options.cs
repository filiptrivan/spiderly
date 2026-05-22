namespace Spiderly.Shared
{
    /// <summary>
    /// S3-compatible object storage options. Bound from the <c>AppSettings:Spiderly.Shared</c>
    /// configuration section and injected into the storage services as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    public class S3Options
    {
        /// <summary>Name of the S3 bucket blobs are stored in.</summary>
        public string S3BucketName { get; set; }

        /// <summary>Public base URL used to build externally reachable URLs for public blobs.</summary>
        public string S3PublicEndpoint { get; set; }
    }
}
