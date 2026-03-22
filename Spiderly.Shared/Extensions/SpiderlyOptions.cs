namespace Spiderly.Shared.Extensions
{
    /// <summary>
    /// Records which Spiderly features were registered during ConfigureServices.
    /// Used by <see cref="StartupExtensions.UseSpiderly"/> to conditionally activate middleware.
    /// </summary>
    internal class SpiderlyOptions
    {
        internal bool AuthenticationEnabled { get; set; }
        internal bool SwaggerEnabled { get; set; }
        internal bool RateLimitingEnabled { get; set; }
    }
}
