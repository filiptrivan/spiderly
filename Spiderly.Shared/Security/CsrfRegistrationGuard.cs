using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Spiderly.Shared.Security
{
    /// <summary>
    /// Records whether <c>UseSpiderlyCsrf()</c> was called, so a consumer who forgets it fails at boot instead of
    /// serving cookie-authenticated writes with no CSRF protection. Same fail-loud posture as
    /// <c>PermissionHandlerRegistrationGuard</c>: a security layer must never be able to go missing quietly.
    /// </summary>
    /// <remarks>
    /// A middleware registration cannot be inspected the way a DI registration can — the pipeline is a delegate
    /// chain, not a service collection — so <c>UseSpiderlyCsrf()</c> marks this singleton and the startup filter
    /// asserts the mark <b>after</b> the consumer's <c>Configure</c> has run and built the pipeline.
    /// </remarks>
    public sealed class CsrfRegistrationGuard : IStartupFilter
    {
        /// <summary>True once <c>UseSpiderlyCsrf()</c> has added the middleware.</summary>
        public bool IsRegistered { get; private set; }

        /// <summary>Marks the middleware as registered. Called by <c>UseSpiderlyCsrf()</c>.</summary>
        public void MarkRegistered() => IsRegistered = true;

        /// <inheritdoc/>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);

                if (IsRegistered == false)
                    throw new InvalidOperationException(
                        "Spiderly authentication is enabled and issues cookie-based access tokens, but " +
                        "UseSpiderlyCsrf() was never called — so a state-changing request authenticated by an " +
                        "ambient cookie is accepted with no CSRF check, from any origin the CORS policy admits. " +
                        "Add app.UseSpiderlyCsrf() after UseRouting() (it reads endpoint metadata for the " +
                        "[IgnoreCsrf] opt-out) and before UseAuthorization(); the spiderly init template includes " +
                        "this call.");
            };
        }
    }
}
