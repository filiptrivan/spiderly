using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Localization;
using Spiderly.Shared.Services;

namespace Spiderly.Shared.Tests;

/// <summary>
/// Pins the customer-facing error reference: <c>ApiErrorDTO.TraceId</c> is present exactly when the
/// exception is reportable (<see cref="SpiderlyExceptionClassifier.IsExpected"/> == false), so a reference
/// a user reads to support always points at a findable event and an expected 4xx never looks like an incident.
/// </summary>
public class SpiderlyExceptionHandlerTests
{
    private static SpiderlyExceptionHandler CreateHandler()
    {
        return new SpiderlyExceptionHandler(
            NullLogger<SpiderlyExceptionHandler>.Instance,
            new PassthroughStringLocalizer(),
            new ProductionEnvironment(),
            Options.Create(new TokenKeyOptions()),
            new CookieManager(Options.Create(new CookieSettings())),
            new SpiderlyPrincipalAccessor(new HttpContextAccessor()));
    }

    private static async Task<(int StatusCode, JsonDocument Body)> HandleAsync(Exception exception)
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        bool handled = await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);

        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body);
        return (context.Response.StatusCode, JsonDocument.Parse(await reader.ReadToEndAsync()));
    }

    public static TheoryData<Exception, int> ReportableExceptions => new()
    {
        { new InvalidOperationException("boom"), StatusCodes.Status500InternalServerError },
        { new SecurityViolationException(), StatusCodes.Status403Forbidden },
    };

    [Theory]
    [MemberData(nameof(ReportableExceptions))]
    public async Task Reportable_error_carries_the_current_trace_id(Exception exception, int expectedStatus)
    {
        using Activity activity = new Activity("test-request").Start();

        (int status, JsonDocument body) = await HandleAsync(exception);

        Assert.Equal(expectedStatus, status);
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
        TestActivities.StopAmbient();

        (int status, JsonDocument body) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.False(body.RootElement.TryGetProperty("traceId", out _));
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
}
