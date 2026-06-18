using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Authorization;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // Keystone of the transport-agnostic principal model: identity is read from this accessor (AsyncLocal,
    // with an HTTP fallback) instead of HttpContext directly, so the same code resolves identity under HTTP,
    // background jobs, and tests. These tests pin: the anonymous default (no NRE when there's no HttpContext —
    // closes the Hangfire landmine), explicit push/restore precedence, nesting, and the HTTP claims fallback.
    public class SpiderlyPrincipalAccessorTests
    {
        // No HttpContext (e.g. a background job) and nothing pushed → Anonymous, never a throw. This is the
        // landmine fix: the old AuthenticationService read HttpContext directly and NRE'd in non-HTTP contexts.
        [Fact]
        public void Current_is_Anonymous_when_no_http_context_and_nothing_pushed()
        {
            SpiderlyPrincipalAccessor accessor = new(new HttpContextAccessor());

            SpiderlyPrincipal current = accessor.Current;

            Assert.NotNull(current);
            Assert.Null(current.UserId);
            Assert.False(current.IsAuthenticated);
            Assert.False(current.IsSystem);
        }

        [Fact]
        public void Push_makes_principal_current_and_restores_previous_on_dispose()
        {
            SpiderlyPrincipalAccessor accessor = new(new HttpContextAccessor());

            using (accessor.Push(SpiderlyPrincipal.ForUser(42, "User")))
            {
                Assert.Equal(42, accessor.Current.UserId);
                Assert.Equal("User", accessor.Current.Kind);
                Assert.True(accessor.Current.IsAuthenticated);
            }

            // Scope disposed → back to the empty baseline.
            Assert.Null(accessor.Current.UserId);
            Assert.False(accessor.Current.IsAuthenticated);
        }

        [Fact]
        public void Push_nests_and_each_dispose_restores_the_enclosing_principal()
        {
            SpiderlyPrincipalAccessor accessor = new(new HttpContextAccessor());

            using (accessor.Push(SpiderlyPrincipal.ForUser(1)))
            {
                Assert.Equal(1, accessor.Current.UserId);

                using (accessor.Push(SpiderlyPrincipal.ForUser(2)))
                {
                    Assert.Equal(2, accessor.Current.UserId);
                }

                Assert.Equal(1, accessor.Current.UserId);
            }

            Assert.Null(accessor.Current.UserId);
        }

        [Fact]
        public void System_principal_has_no_user_but_flags_IsSystem()
        {
            SpiderlyPrincipalAccessor accessor = new(new HttpContextAccessor());

            using (accessor.Push(SpiderlyPrincipal.System))
            {
                Assert.Null(accessor.Current.UserId);
                Assert.False(accessor.Current.IsAuthenticated);
                Assert.True(accessor.Current.IsSystem);
            }
        }

        // The HTTP adapter: when nothing is pushed, identity is resolved from the authenticated request's claims.
        [Fact]
        public void Current_falls_back_to_authenticated_http_context_claims()
        {
            HttpContextAccessor httpContextAccessor = new()
            {
                HttpContext = BuildAuthenticatedContext(userId: 7, kind: "User"),
            };
            SpiderlyPrincipalAccessor accessor = new(httpContextAccessor);

            Assert.Equal(7, accessor.Current.UserId);
            Assert.Equal("User", accessor.Current.Kind);
            Assert.True(accessor.Current.IsAuthenticated);
        }

        // An explicitly pushed principal wins over the ambient HTTP context (e.g. impersonation / a job filter).
        [Fact]
        public void Pushed_principal_takes_precedence_over_http_context()
        {
            HttpContextAccessor httpContextAccessor = new()
            {
                HttpContext = BuildAuthenticatedContext(userId: 7, kind: "User"),
            };
            SpiderlyPrincipalAccessor accessor = new(httpContextAccessor);

            using (accessor.Push(SpiderlyPrincipal.System))
            {
                Assert.True(accessor.Current.IsSystem);
                Assert.Null(accessor.Current.UserId);
            }

            // After dispose, the HTTP fallback applies again.
            Assert.Equal(7, accessor.Current.UserId);
        }

        [Fact]
        public void Current_is_Anonymous_when_http_context_is_unauthenticated()
        {
            HttpContextAccessor httpContextAccessor = new() { HttpContext = new DefaultHttpContext() };
            SpiderlyPrincipalAccessor accessor = new(httpContextAccessor);

            Assert.Null(accessor.Current.UserId);
            Assert.False(accessor.Current.IsAuthenticated);
        }

        [Fact]
        public void Push_null_throws()
        {
            SpiderlyPrincipalAccessor accessor = new(new HttpContextAccessor());

            Assert.Throws<ArgumentNullException>(() => accessor.Push(null));
        }

        private static DefaultHttpContext BuildAuthenticatedContext(long userId, string kind)
        {
            List<Claim> claims = new()
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(PrincipalClaims.PrincipalKind, kind),
            };

            // A non-null authentication type makes ClaimsIdentity.IsAuthenticated return true.
            ClaimsIdentity identity = new(claims, authenticationType: "Test");
            return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        }
    }
}
