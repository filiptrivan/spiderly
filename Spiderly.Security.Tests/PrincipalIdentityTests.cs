using Microsoft.Extensions.Options;
using Spiderly.Security.Services;
using Spiderly.Shared.Authorization;
using Spiderly.Shared;
using Spiderly.Shared.Services;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// A machine principal must never be readable as a human user's identity.
    ///
    /// The bug this pins: an API key is a first-class principal whose subject is the <c>ApiKey.Id</c>, and
    /// <c>GetCurrentUserId()</c> returned that id with no regard for kind. Ids come from independent sequences,
    /// so a key would resolve to "the user whose Id happens to equal this key's Id" — and every identity-scoped
    /// endpoint (my-orders, addresses, wishlist) filters on exactly that value. That is broken object-level
    /// authorization by principal confusion, and it needs no permission at all: the permission ceiling only
    /// governs permission-gated endpoints, which identity-scoped storefront endpoints are not.
    /// </summary>
    public class PrincipalIdentityTests
    {
        private static IPrincipalRegistry Registry() => new PrincipalRegistry(
        [
            new FakeResolver(PrincipalKinds.User, PrincipalNature.Human),
            new FakeResolver(PrincipalKinds.ApiKey, PrincipalNature.Machine),
        ]);

        [Fact]
        public void Machine_principal_is_not_readable_as_a_user_id()
        {
            PrincipalIdentity identity = new(Registry());

            Assert.Throws<PrincipalKindMismatchException>(
                () => identity.GetUserId(SpiderlyPrincipal.ForPrincipal(7, PrincipalKinds.ApiKey)));
        }

        // An unregistered kind is not evidence of a human — it is evidence of a misconfiguration or a forged
        // claim, so it must refuse rather than fall through to "probably a user".
        [Fact]
        public void Unregistered_kind_fails_closed()
        {
            PrincipalIdentity identity = new(Registry());

            Assert.Throws<PrincipalKindMismatchException>(
                () => identity.GetUserId(SpiderlyPrincipal.ForPrincipal(7, "SomethingElse")));
        }

        // The single-principal case: one registered kind means requests carry no principal_kind claim at all,
        // and that lone kind is the default. A null kind must therefore still resolve — otherwise every
        // single-principal app (the common case) breaks on its own identity reads.
        [Fact]
        public void Null_kind_resolves_to_the_single_registered_kind()
        {
            PrincipalIdentity identity = new(new PrincipalRegistry(
                [new FakeResolver(PrincipalKinds.User, PrincipalNature.Human)]));

            Assert.Equal(7, identity.GetUserId(SpiderlyPrincipal.ForPrincipal(7)));
        }

        [Fact]
        public void Human_principal_resolves_to_its_user_id()
        {
            PrincipalIdentity identity = new(Registry());

            Assert.Equal(7, identity.GetUserId(SpiderlyPrincipal.ForPrincipal(7, PrincipalKinds.User)));
        }

        // The rule only protects anything if the accessor consumers actually call routes through it. Every
        // identity-scoped read in a Spiderly app reaches identity via AuthenticationService, so that is where
        // the behaviour is asserted — not just on the pure resolver above.
        [Fact]
        public void AuthenticationService_refuses_a_user_id_for_a_machine_principal()
        {
            AuthenticationService service = ServiceFor(SpiderlyPrincipal.ForPrincipal(7, PrincipalKinds.ApiKey));

            Assert.Throws<PrincipalKindMismatchException>(() => service.GetCurrentUserId());
        }

        [Fact]
        public void AuthenticationService_returns_the_user_id_for_a_human_principal()
        {
            AuthenticationService service = ServiceFor(SpiderlyPrincipal.ForPrincipal(7, PrincipalKinds.User));

            Assert.Equal(7, service.GetCurrentUserId());
        }

        // Soft paths (a nullable read on an endpoint that may run anonymously) must degrade, not 500: a machine
        // principal simply has no user id, which is the same shape as "nobody is logged in".
        [Fact]
        public void AuthenticationService_reports_no_user_id_for_a_machine_principal_on_the_nullable_read()
        {
            AuthenticationService service = ServiceFor(SpiderlyPrincipal.ForPrincipal(7, PrincipalKinds.ApiKey));

            Assert.Null(service.GetCurrentUserIdOrDefault());
        }

        [Fact]
        public void AuthenticationService_exposes_the_principal_id_for_a_machine_principal()
        {
            AuthenticationService service = ServiceFor(SpiderlyPrincipal.ForPrincipal(7, PrincipalKinds.ApiKey));

            Assert.Equal(7, service.GetCurrentPrincipalId());
        }

        private static AuthenticationService ServiceFor(SpiderlyPrincipal principal)
        {
            return new AuthenticationService(
                httpContextAccessor: null!,
                principalAccessor: new StaticPrincipalAccessor(principal),
                context: null!,
                localizer: null!,
                authPolicyOptions: Options.Create(new AuthPolicyOptions()),
                tokenKeyOptions: Options.Create(new TokenKeyOptions()),
                cookieManager: new CookieManager(Options.Create(new CookieSettings())),
                principalIdentity: new PrincipalIdentity(Registry()));
        }

        private sealed class StaticPrincipalAccessor(SpiderlyPrincipal principal) : ISpiderlyPrincipalAccessor
        {
            public SpiderlyPrincipal Current { get; } = principal;

            public IDisposable Push(SpiderlyPrincipal principal) =>
                throw new NotSupportedException("The test accessor is a fixed snapshot.");
        }

        private sealed class FakeResolver(string kind, PrincipalNature nature) : IPrincipalPermissionResolver
        {
            public string Kind { get; } = kind;

            public PrincipalNature Nature { get; } = nature;

            public Task<bool> HasPermissionAsync(
                Spiderly.Shared.Interfaces.IApplicationDbContext context, long principalId, string permissionCode) =>
                Task.FromResult(false);

            public Task<List<string>> GetPermissionCodesAsync(
                Spiderly.Shared.Interfaces.IApplicationDbContext context, long principalId) =>
                Task.FromResult(new List<string>());
        }
    }
}
