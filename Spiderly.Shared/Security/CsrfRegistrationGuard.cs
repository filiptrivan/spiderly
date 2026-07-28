using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Spiderly.Shared.Security
{
    /// <summary>
    /// Fails the boot when <c>UseSpiderlyCsrf()</c> was never called, so a consumer who forgets it cannot serve
    /// cookie-authenticated writes with no CSRF protection. Same fail-loud posture as
    /// <c>PermissionHandlerRegistrationGuard</c>: a security layer must never be able to go missing quietly.
    /// </summary>
    /// <remarks>
    /// <para>The marker lives in <see cref="IApplicationBuilder.Properties"/>, which is how ASP.NET Core itself
    /// solves this — <c>UseAuthorization</c> stamps a key that <c>UseEndpoints</c> reads and throws on. A
    /// middleware registration is a delegate chain, not an inspectable service collection, so some marker is
    /// unavoidable; this one is the framework's own idiom.</para>
    /// <para>Deliberately NOT a mutable DI singleton, which is what this was first written as. A singleton is
    /// resolved from the root provider, so <c>app.Map("/x", b =&gt; b.UseSpiderlyCsrf())</c> would mark it
    /// globally and the guard would pass while the main pipeline stayed unprotected. Branch builders get a COPY
    /// of <c>Properties</c>, so a branch-only registration leaves the root unmarked and this fails — which is the
    /// correct answer. It also stops the flag being settable by anything that can resolve a service.</para>
    /// </remarks>
    public sealed class CsrfRegistrationGuard : IStartupFilter
    {
        /// <summary>Key stamped into <see cref="IApplicationBuilder.Properties"/> by <c>UseSpiderlyCsrf()</c>.</summary>
        public const string RegisteredKey = "Spiderly.CsrfMiddlewareRegistered";

        /// <inheritdoc/>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);

                if (app.Properties.ContainsKey(RegisteredKey) == false)
                    throw new InvalidOperationException(
                        "Spiderly authentication is enabled and issues cookie-based access tokens, but " +
                        "UseSpiderlyCsrf() was never called on the application pipeline — so a state-changing " +
                        "request authenticated by an ambient cookie is accepted with no CSRF check, from any " +
                        "origin the CORS policy admits. Add app.UseSpiderlyCsrf() after UseRouting() (it reads " +
                        "endpoint metadata for the [IgnoreCsrf] opt-out) and before UseAuthorization(); the " +
                        "spiderly init template includes this call. If you registered it only inside an " +
                        "app.Map(...) branch, move it to the main pipeline — a branch does not protect the rest.");
            };
        }
    }
}
