using Microsoft.AspNetCore.Authorization;
using Spiderly.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Security;

namespace Spiderly.Shared.Tests;

/// <summary>
/// Pins the CSRF decision surface. The check moved out of <c>[AuthGuard]</c> into middleware precisely so it
/// stops being per-endpoint opt-in, so the load-bearing assertions are the ones about requests that carry NO
/// authorization attribute at all — under the old attribute-based scheme those were silently unprotected.
/// </summary>
public class SpiderlyCsrfMiddlewareTests
{
    private const string AccessTokenKey = "access_token";

    private static HttpContext Request(
        string method,
        bool withCookie = false,
        bool withBearer = false,
        bool withCsrfHeader = false,
        bool ignoreCsrf = false)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;

        if (withCookie)
            context.Request.Headers["Cookie"] = $"{AccessTokenKey}=abc123";

        if (withBearer)
            context.Request.Headers["Authorization"] = "Bearer abc123";

        if (withCsrfHeader)
            context.Request.Headers[SpiderlyCsrfMiddleware.HeaderName] = "1";

        if (ignoreCsrf)
        {
            context.Features.Set<IEndpointFeature>(new EndpointFeatureStub(
                new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new IgnoreCsrfAttribute()), "ignored")));
        }

        return context;
    }

    private sealed class EndpointFeatureStub(Endpoint endpoint) : IEndpointFeature
    {
        public Endpoint Endpoint { get; set; } = endpoint;
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void Cookie_authenticated_state_changing_request_requires_the_header(string method)
    {
        Assert.True(SpiderlyCsrfMiddleware.RequiresCsrfHeader(Request(method, withCookie: true), AccessTokenKey));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void Safe_methods_are_never_challenged(string method)
    {
        Assert.False(SpiderlyCsrfMiddleware.RequiresCsrfHeader(Request(method, withCookie: true), AccessTokenKey));
    }

    [Fact]
    public void Bearer_authenticated_request_is_not_challenged()
    {
        // A token in an Authorization header is not attached ambiently by the browser, so there is nothing to forge.
        Assert.False(SpiderlyCsrfMiddleware.RequiresCsrfHeader(Request("POST", withBearer: true), AccessTokenKey));
    }

    [Fact]
    public void Bearer_wins_when_both_credentials_are_present()
    {
        Assert.False(SpiderlyCsrfMiddleware.RequiresCsrfHeader(
            Request("POST", withCookie: true, withBearer: true), AccessTokenKey));
    }

    [Fact]
    public void Anonymous_request_is_not_challenged()
    {
        Assert.False(SpiderlyCsrfMiddleware.RequiresCsrfHeader(Request("POST"), AccessTokenKey));
    }

    [Fact]
    public void IgnoreCsrf_endpoint_opts_out()
    {
        Assert.False(SpiderlyCsrfMiddleware.RequiresCsrfHeader(
            Request("POST", withCookie: true, ignoreCsrf: true), AccessTokenKey));
    }

    [Fact]
    public async Task Missing_header_is_rejected_with_403_and_the_pipeline_stops()
    {
        HttpContext context = Request("POST", withCookie: true);
        bool nextRan = false;

        SpiderlyCsrfMiddleware middleware = new(
            _ => { nextRan = true; return Task.CompletedTask; },
            Microsoft.Extensions.Options.Options.Create(new TokenKeyOptions()));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextRan);
    }

    [Fact]
    public async Task Present_header_passes_through()
    {
        HttpContext context = Request("POST", withCookie: true, withCsrfHeader: true);
        bool nextRan = false;

        SpiderlyCsrfMiddleware middleware = new(
            _ => { nextRan = true; return Task.CompletedTask; },
            Microsoft.Extensions.Options.Options.Create(new TokenKeyOptions()));

        await middleware.InvokeAsync(context);

        Assert.True(nextRan);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    // The regression that motivated moving CSRF out of the attribute: an endpoint carrying no authorization
    // attribute at all used to get NO csrf check, because the check rode on [AuthGuard]. It is now covered by
    // construction — the request shape decides, not the endpoint's annotations.
    [Fact]
    public void Endpoint_with_no_authorization_attribute_is_still_challenged()
    {
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Headers["Cookie"] = $"{AccessTokenKey}=abc123";
        context.Features.Set<IEndpointFeature>(new EndpointFeatureStub(
            new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "unannotated")));

        Assert.True(SpiderlyCsrfMiddleware.RequiresCsrfHeader(context, AccessTokenKey));
    }
}

/// <summary>Pins the merged guard's two shapes and its platform integration.</summary>
public class AuthGuardAttributeTests
{
    [Fact]
    public void Bare_guard_requires_authentication_without_a_policy()
    {
        AuthGuardAttribute guard = new();

        Assert.Null(guard.PermissionCode);
        Assert.Null(guard.Policy);
    }

    [Fact]
    public void Guard_with_a_code_maps_to_the_permission_policy()
    {
        AuthGuardAttribute guard = new("UpdateProduct");

        Assert.Equal("UpdateProduct", guard.PermissionCode);
        Assert.Equal(SpiderlyAuthorizationPolicies.ForPermission("UpdateProduct"), guard.Policy);
    }

    // Deriving from AuthorizeAttribute is what buys class+action composition, [AllowAnonymous], and OpenAPI
    // visibility for free — a hand-rolled filter would get none of it.
    [Fact]
    public void Guard_is_an_AuthorizeAttribute()
    {
        Assert.IsAssignableFrom<AuthorizeAttribute>(new AuthGuardAttribute());
    }

    [Fact]
    public void Empty_permission_code_is_rejected_rather_than_silently_meaning_authenticated_only()
    {
        Assert.Throws<ArgumentException>(() => new AuthGuardAttribute(""));
        Assert.Throws<ArgumentException>(() => new AuthGuardAttribute(null));
    }
}

/// <summary>
/// The guard exists so a missing <c>UseSpiderlyCsrf()</c> is a loud boot failure rather than silently
/// unprotected cookie-authenticated writes.
/// </summary>
public class CsrfRegistrationGuardTests
{
    private static IApplicationBuilder Builder() =>
        new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

    [Fact]
    public void Boot_fails_when_the_middleware_was_never_registered()
    {
        IApplicationBuilder app = Builder();
        Action<IApplicationBuilder> pipeline = new CsrfRegistrationGuard().Configure(_ => { });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => pipeline(app));
        Assert.Contains("UseSpiderlyCsrf()", ex.Message);
    }

    [Fact]
    public void Boot_succeeds_when_the_middleware_is_registered_on_the_main_pipeline()
    {
        IApplicationBuilder app = Builder();
        Action<IApplicationBuilder> pipeline = new CsrfRegistrationGuard()
            .Configure(builder => builder.UseSpiderlyCsrf());

        pipeline(app);
    }

    // The reason this guard reads IApplicationBuilder.Properties instead of a DI singleton: a branch builder
    // gets a COPY of Properties, so registering only inside app.Map(...) leaves the main pipeline unprotected.
    // A singleton flag would have been set globally and the guard would have passed.
    [Fact]
    public void Boot_fails_when_the_middleware_is_only_registered_inside_a_branch()
    {
        IApplicationBuilder app = Builder();
        Action<IApplicationBuilder> pipeline = new CsrfRegistrationGuard()
            .Configure(builder => builder.Map("/branch", branch => branch.UseSpiderlyCsrf()));

        Assert.Throws<InvalidOperationException>(() => pipeline(app));
    }
}
