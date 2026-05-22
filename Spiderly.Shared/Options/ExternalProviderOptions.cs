namespace Spiderly.Shared
{
    /// <summary>
    /// External-auth-provider options. Bound from the <c>AppSettings:Spiderly.Shared</c> configuration
    /// section and injected into the application DbContext as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>, so model shaping depends on configuration.
    /// </summary>
    public class ExternalProviderOptions
    {
        /// <summary>
        /// When <c>false</c>, the Google external-provider column is omitted from the user model.
        /// </summary>
        public bool UseGoogleAsExternalProvider { get; set; } = true;
    }
}
