namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the external-auth-provider settings. Implemented by <see cref="Settings"/> and
    /// injected into the application DbContext, so model shaping depends on configuration rather than a
    /// global mutable static.
    /// </summary>
    public interface IExternalProviderSettings
    {
        /// <summary>
        /// When <c>false</c>, the Google external-provider column is omitted from the user model.
        /// </summary>
        bool UseGoogleAsExternalProvider { get; }
    }
}
