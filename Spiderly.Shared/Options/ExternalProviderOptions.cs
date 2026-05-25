using System.Collections.Generic;

namespace Spiderly.Shared
{
    /// <summary>
    /// External-auth-provider options. Bound from the <c>AppSettings:Spiderly.Shared</c> configuration
    /// section and injected as <see cref="Microsoft.Extensions.Options.IOptions{T}"/> — into the security
    /// services (to build the provider registry and validate id tokens) and into the DbContext (to shape
    /// the user model).
    /// </summary>
    public class ExternalProviderOptions
    {
        /// <summary>
        /// The set of enabled external authentication providers. Each entry is validated by Spiderly's
        /// generic OIDC validator (or a consumer-supplied <see cref="Spiderly.Shared.ExternalAuth.IExternalAuthProvider"/>
        /// registered for the same <see cref="ExternalProviderConfig.Code"/>).
        /// </summary>
        public List<ExternalProviderConfig> ExternalProviders { get; set; } = new();
    }
}
