namespace Spiderly.Shared.Constants
{
    /// <summary>
    /// Named rate-limit policies owned by Spiderly. Registered by
    /// <c>SpiderlyAddRateLimiters</c> when <c>AddRateLimiting()</c> is called.
    /// Applications may redefine these policies in their own <c>Configure&lt;RateLimiterOptions&gt;</c>
    /// call to tune the limits without forking Spiderly.
    /// </summary>
    public static class SpiderlyRateLimitPolicies
    {
        /// <summary>Applied to generated <c>Upload*For*</c> endpoints.</summary>
        public const string BlobUpload = "spiderly:blob-upload";
    }
}
