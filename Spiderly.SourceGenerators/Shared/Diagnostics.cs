using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Shared
{
    public static class SpiderlyDescriptors
    {
        public static readonly DiagnosticDescriptor UnhandledException = new(
            id: "SPDR0001",
            title: "Unhandled Exception in Source Generator",
            messageFormat: "Exception in '{0}': {1}",
            category: "Spiderly.SourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor InvalidDisplayNameUsage = new(
            id: "SPDR0002",
            title: "Invalid DisplayName Usage",
            messageFormat: "DisplayName attribute must contain a non-empty string.",
            category: "Spiderly.Validation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnknownSpiderlyAttribute = new(
            id: "SPDR0003",
            title: "Unknown Spiderly Attribute",
            messageFormat: "The attribute '{0}' is not recognized by Spiderly.",
            category: "Spiderly.Validation",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
    }

    public static class Diagnostics
    {
        public static void ReportException(SourceProductionContext context, string generatorName, Exception exception)
        {
            Location location = Location.None;

            // If a custom SpiderlySourceGeneratorException is thrown, prefer its method name
            string message = exception is SpiderlySourceGeneratorException spiderlyEx
                ? $"Exception in {spiderlyEx.MethodName}: {spiderlyEx.Message}"
                : exception.Message;

            var trace = new StackTrace(exception, true);
            var frame = trace.GetFrames()?.FirstOrDefault(f =>
                !string.IsNullOrEmpty(f.GetFileName()) &&
                f.GetFileLineNumber() != 0
            );

            if (frame != null)
            {
                var lineSpan = FileLinePositionSpanFrom(frame);
                location = Location.Create(frame.GetFileName(), TextSpan.FromBounds(0, 0), lineSpan.Span);
            }

            context.ReportDiagnostic(Diagnostic.Create(
                SpiderlyDescriptors.UnhandledException,
                location,
                generatorName,
                message
            ));
        }

        private static FileLinePositionSpan FileLinePositionSpanFrom(StackFrame frame)
        {
            return new FileLinePositionSpan(
                frame.GetFileName(),
                new LinePositionSpan(
                    new LinePosition(frame.GetFileLineNumber() - 1, 0),
                    new LinePosition(frame.GetFileLineNumber() - 1, 100)
                )
            );
        }

        public static void ReportInvalidDisplayName(SourceProductionContext context, Location location)
        {
            var diagnostic = Diagnostic.Create(
                SpiderlyDescriptors.InvalidDisplayNameUsage,
                location
            );

            context.ReportDiagnostic(diagnostic);
        }

        public static void ReportUnknownSpiderlyAttribute(SourceProductionContext context, Location location, string attributeName)
        {
            var diagnostic = Diagnostic.Create(
                SpiderlyDescriptors.UnknownSpiderlyAttribute,
                location,
                attributeName
            );

            context.ReportDiagnostic(diagnostic);
        }
    }
}
