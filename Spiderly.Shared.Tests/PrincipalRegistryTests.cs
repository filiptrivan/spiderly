using Spiderly.Shared.Authorization;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="PrincipalRegistry"/> — the kind → resolver lookup the authorization core
    /// dispatches through. Covers the single-kind default (so single-principal apps need no <c>principal_kind</c>
    /// claim), fail-closed resolution for an ambiguous/unknown/absent kind, and the clear duplicate-kind guard.
    /// </summary>
    public class PrincipalRegistryTests
    {
        [Fact]
        public void Single_kind_is_the_default_and_resolves_case_insensitively()
        {
            PrincipalRegistry registry = new(new[] { Resolver("User") });

            Assert.False(registry.IsEmpty);
            Assert.Equal("User", registry.DefaultKind);

            Assert.True(registry.TryResolve(null, out IPrincipalPermissionResolver? byDefault));  // no claim → default
            Assert.Equal("User", byDefault.Kind);
            Assert.True(registry.TryResolve("", out _));
            Assert.True(registry.TryResolve("user", out _));                                     // claim casing must not matter
        }

        [Fact]
        public void Multiple_kinds_have_no_default_and_require_the_claim()
        {
            PrincipalRegistry registry = new(new[] { Resolver("User"), Resolver("ServiceAccount") });

            Assert.Null(registry.DefaultKind);

            Assert.True(registry.TryResolve("ServiceAccount", out IPrincipalPermissionResolver? resolved));
            Assert.Equal("ServiceAccount", resolved.Kind);

            // No claim while ambiguous → fail closed (deny), not throw.
            Assert.False(registry.TryResolve(null, out IPrincipalPermissionResolver? none));
            Assert.Null(none);
        }

        [Fact]
        public void Unknown_kind_resolves_to_false()
        {
            PrincipalRegistry registry = new(new[] { Resolver("User") });

            Assert.False(registry.TryResolve("Robot", out IPrincipalPermissionResolver? resolver));
            Assert.Null(resolver);
        }

        [Fact]
        public void Empty_registry_is_empty_and_resolves_to_false()
        {
            PrincipalRegistry registry = new(Array.Empty<IPrincipalPermissionResolver>());

            Assert.True(registry.IsEmpty);
            Assert.Null(registry.DefaultKind);
            Assert.False(registry.TryResolve(null, out _));
            Assert.False(registry.TryResolve("User", out _));
        }

        [Fact]
        public void Duplicate_kind_registration_throws_a_clear_error()
        {
            // Exact duplicate.
            InvalidOperationException exact = Assert.Throws<InvalidOperationException>(
                () => new PrincipalRegistry(new[] { Resolver("User"), Resolver("User") }));
            Assert.Contains("User", exact.Message);
            Assert.Contains("more than once", exact.Message);

            // Case-variant duplicate (kinds are matched case-insensitively).
            Assert.Throws<InvalidOperationException>(
                () => new PrincipalRegistry(new[] { Resolver("User"), Resolver("user") }));
        }

        private static IPrincipalPermissionResolver Resolver(
            string kind, PrincipalNature nature = PrincipalNature.Human) => new FakeResolver(kind, nature);

        // Only Kind and Nature participate in routing; the permission methods are never reached in these tests.
        private sealed class FakeResolver : IPrincipalPermissionResolver
        {
            public FakeResolver(string kind, PrincipalNature nature)
            {
                Kind = kind;
                Nature = nature;
            }

            public string Kind { get; }
            public PrincipalNature Nature { get; }
            public Task<bool> HasPermissionAsync(IApplicationDbContext context, long principalId, string permissionCode) => throw new NotSupportedException();
            public Task<List<string>> GetPermissionCodesAsync(IApplicationDbContext context, long principalId) => throw new NotSupportedException();
        }
    }
}
