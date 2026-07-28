using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Shared;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Services;

namespace Spiderly.Shared.Tests;

/// <summary>
/// Pins the customer-facing error reference: the response body carries the W3C trace id exactly when the
/// exception is one the error tracker keeps (<see cref="SpiderlyExceptionClassifier.IsExpected"/> == false),
/// so a reference a user reads to support always points at a findable event — and an expected 4xx never
/// looks like an incident.
/// </summary>
public class SpiderlyExceptionHandlerTests
{
    private static SpiderlyExceptionHandler CreateHandler()
    {
        return new SpiderlyExceptionHandler(
            NullLogger<SpiderlyExceptionHandler>.Instance,
            new PassthroughLocalizer(),
            new ProductionEnvironment(),
            Options.Create(new TokenKeyOptions()),
            new CookieManager(Options.Create(new CookieSettings())),
            new AnonymousPrincipalAccessor());
    }

    private static async Task<(int StatusCode, JsonDocument Body)> HandleAsync(Exception exception)
    {
        DefaultHttpContext context = new();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        bool handled = await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);

        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body);
        return (context.Response.StatusCode, JsonDocument.Parse(await reader.ReadToEndAsync()));
    }

    private static void StopAmbientActivities()
    {
        while (Activity.Current != null)
            Activity.Current.Stop();
    }

    [Fact]
    public async Task Unexpected_500_carries_the_current_trace_id()
    {
        using Activity activity = new Activity("test-request").Start();

        (int status, JsonDocument body) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal(activity.TraceId.ToString(), body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Security_violation_403_carries_the_current_trace_id()
    {
        using Activity activity = new Activity("test-request").Start();

        (int status, JsonDocument body) = await HandleAsync(new SecurityViolationException());

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(activity.TraceId.ToString(), body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Expected_business_exception_omits_the_trace_id()
    {
        using Activity activity = new Activity("test-request").Start();

        (int status, JsonDocument body) = await HandleAsync(new BusinessException("Out of stock."));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.False(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Unexpected_500_without_an_ambient_activity_omits_the_trace_id()
    {
        StopAmbientActivities();

        (int status, JsonDocument body) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.False(body.RootElement.TryGetProperty("traceId", out _));
    }

    private sealed class PassthroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class ProductionEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Spiderly.Shared.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class AnonymousPrincipalAccessor : ISpiderlyPrincipalAccessor
    {
        public SpiderlyPrincipal Current => SpiderlyPrincipal.Anonymous;
        public IDisposable Push(SpiderlyPrincipal principal) => throw new NotSupportedException();
    }
}
