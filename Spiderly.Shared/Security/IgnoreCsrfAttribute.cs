using System;

namespace Spiderly.Shared.Security
{
    /// <summary>
    /// Opts an endpoint out of <see cref="SpiderlyCsrfMiddleware"/>. Mirrors ASP.NET Core's
    /// <c>IgnoreAntiforgeryToken</c>: protection is global, exemptions are explicit and few.
    /// </summary>
    /// <remarks>
    /// Legitimate uses are endpoints a browser never reaches with the user's cookies attached and that authenticate
    /// by other means — a payment-gateway callback verifying a signed payload, a partner webhook. If you reach for
    /// this because a first-party client "can't send the header", fix the client instead: the exemption removes the
    /// only thing standing between that endpoint and a cross-site forged request.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class IgnoreCsrfAttribute : Attribute
    {
    }
}
