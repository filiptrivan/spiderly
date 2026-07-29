using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using Xunit;

namespace Spiderly.SourceGenerators.Tests;

/// <summary>
/// A generator that faults on an unexpected exception surfaces as Roslyn's CS8785: warning-level, no
/// location, and the generator silently contributes nothing — so the build stays green while generated
/// code the consumer was promised simply isn't there. In a real app that resurfaces later as a pile of
/// CS0246s with no hint a generator died; in a framework project it produces nothing and goes unnoticed
/// (Spiderly.Security carried exactly that for months). The wrapper must turn any escape into a named,
/// build-failing Spiderly diagnostic.
/// </summary>
public class GeneratorFaultDiagnosticTests
{
    private const string AnySource = """
        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Item : BusinessObject<long>
            {
                public string Name { get; set; }
            }
        }
        """;

    [Fact]
    public void UnexpectedException_IsReportedAsADiagnostic_NotLeftToEscape()
    {
        GeneratorRunResult result = GeneratorTestHarness.Run<ThrowingGenerator>(AnySource)
            .GetRunResult().Results.Single();

        Assert.Null(result.Exception);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SPIDERLY024", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

        // The message has to carry enough to act on: which generator, and what actually went wrong.
        string message = diagnostic.GetMessage();
        Assert.Contains(nameof(ThrowingGenerator), message);
        Assert.Contains(nameof(InvalidOperationException), message);
        Assert.Contains("the boom", message);
    }

    [Fact]
    public void SpiderlyGenerationException_StillReportsItsOwnLocatedDiagnostic()
    {
        // The specific catch must keep winning — a located SPIDERLY003 must not be flattened into the
        // generic fault diagnostic, which would lose both the location and the actionable message.
        GeneratorRunResult result = GeneratorTestHarness.Run<DiagnosticThrowingGenerator>(AnySource)
            .GetRunResult().Results.Single();

        Assert.Null(result.Exception);
        Assert.Equal("SPIDERLY003", Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Cancellation_IsNotDressedUpAsAFault()
    {
        // Roslyn cancels generators routinely (every keystroke in the IDE). Swallowing that into a
        // diagnostic would report a fake build error for ordinary editing.
        GeneratorRunResult result = GeneratorTestHarness.Run<CancellingGenerator>(AnySource)
            .GetRunResult().Results.Single();

        Assert.Empty(result.Diagnostics.Where(d => d.Id == "SPIDERLY024"));
    }

    #region Generators under test

    private sealed class ThrowingGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context) =>
            context.RegisterSafeImplementationSourceOutput(
                context.AnalyzerConfigOptionsProvider.Select((_, _) => 0),
                (_, _) => throw new InvalidOperationException("the boom"));
    }

    private sealed class DiagnosticThrowingGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context) =>
            context.RegisterSafeImplementationSourceOutput(
                context.AnalyzerConfigOptionsProvider.Select((_, _) => 0),
                (_, _) => throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ForeignKeyPropertyNotFound, null, "Missing", "Item", "Nav"));
    }

    private sealed class CancellingGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context) =>
            context.RegisterSafeImplementationSourceOutput(
                context.AnalyzerConfigOptionsProvider.Select((_, _) => 0),
                (_, _) => throw new OperationCanceledException());
    }

    #endregion
}
