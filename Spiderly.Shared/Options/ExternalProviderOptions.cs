namespace Spiderly.Shared
{
    /// <summary>
    /// External-auth-provider options. Bound from the <c>AppSettings:Spiderly.Shared</c> configuration
    /// section and injected as <see cref="Microsoft.Extensions.Options.IOptions{T}"/> — into the DbContext
    /// (to shape the user model) and into the security services (to validate external-provider id tokens).
    /// </summary>
    public class ExternalProviderOptions
    {
        /// <summary>
        /// When <c>false</c>, the Google external-provider column is omitted from the user model.
        /// </summary>
        public bool UseGoogleAsExternalProvider { get; set; } = true;

        /// <summary>Google OAuth client id used to validate external-provider id tokens at login.</summary>
        public string GoogleClientId { get; set; }
    }
}
