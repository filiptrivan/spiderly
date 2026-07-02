using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Authorization;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // Boot-time guard pairing [HasPermission] with its handler. The footgun it codifies: AddSpiderly registers the
    // PermissionPolicyProvider (so [HasPermission] materializes a PermissionRequirement) but the satisfying handler
    // lives in Spiderly.Security and is registered separately by AddSpiderlyAuthorization. A consumer who forgets
    // that call gets a requirement with no handler — which can never Succeed() — so every permission-gated endpoint
    // silently 403s. The guard must turn that into a loud startup failure instead.
    public class PermissionHandlerRegistrationGuardTests
    {
        private sealed class FakePermissionHandler : AuthorizationHandler<PermissionRequirement>
        {
            protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
                => Task.CompletedTask;
        }

        private sealed class OtherRequirement : IAuthorizationRequirement { }

        private sealed class OtherHandler : AuthorizationHandler<OtherRequirement>
        {
            protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OtherRequirement requirement)
                => Task.CompletedTask;
        }

        [Fact]
        public void Configure_throws_when_no_PermissionRequirement_handler_is_registered()
        {
            ServiceCollection services = new();
            // The [HasPermission] provider is in play, but no handler was registered — exactly the drift case.
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

            PermissionHandlerRegistrationGuard guard = new(services);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => guard.Configure(_ => { }));
            Assert.Contains("AddSpiderlyAuthorization", ex.Message);
        }

        [Fact]
        public void Configure_passes_through_when_a_PermissionRequirement_handler_is_registered()
        {
            ServiceCollection services = new();
            services.AddScoped<IAuthorizationHandler, FakePermissionHandler>();

            PermissionHandlerRegistrationGuard guard = new(services);
            Action<IApplicationBuilder> next = _ => { };

            Action<IApplicationBuilder> result = guard.Configure(next);

            Assert.Same(next, result);
        }

        [Fact]
        public void HasPermissionRequirementHandler_ignores_handlers_for_other_requirements()
        {
            ServiceCollection services = new();
            services.AddScoped<IAuthorizationHandler, OtherHandler>();

            Assert.False(PermissionHandlerRegistrationGuard.HasPermissionRequirementHandler(services));
        }

        [Fact]
        public void HasPermissionRequirementHandler_detects_a_handler_for_the_permission_requirement()
        {
            ServiceCollection services = new();
            services.AddScoped<IAuthorizationHandler, FakePermissionHandler>();

            Assert.True(PermissionHandlerRegistrationGuard.HasPermissionRequirementHandler(services));
        }

        [Fact]
        public void HasPermissionRequirementHandler_detects_a_handler_registered_as_an_instance()
        {
            ServiceCollection services = new();
            // Instance registration: ImplementationType is null, but ImplementationInstance carries the runtime type.
            // Without inspecting the instance the guard would falsely fail the boot of a correctly-configured app.
            services.AddSingleton<IAuthorizationHandler>(new FakePermissionHandler());

            Assert.True(PermissionHandlerRegistrationGuard.HasPermissionRequirementHandler(services));
        }

        [Fact]
        public void HasPermissionRequirementHandler_does_not_detect_a_factory_registered_handler()
        {
            ServiceCollection services = new();
            // Factory registration: both ImplementationType and ImplementationInstance are null, so the runtime type
            // is unknowable without invoking the factory (which would instantiate the handler graph at boot). Accepted,
            // documented fail-closed limitation — the resulting false boot failure is loud and rare, and resolving the
            // built provider to close it isn't worth the cost. A consumer on this path uses the AddSecurity bundle.
            services.AddScoped<IAuthorizationHandler>(_ => new FakePermissionHandler());

            Assert.False(PermissionHandlerRegistrationGuard.HasPermissionRequirementHandler(services));
        }
    }
}
