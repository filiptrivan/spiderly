using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Shared
{
    public static class Diagnostics
    {
        private static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
            id: "SPDR0001",
            title: "Unhandled error in Source Generator",
            messageFormat: """Exception in '{0}':\n{1}""",
            category: "Spiderly.SourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static void ReportException(GeneratorExecutionContext context, string generatorName, Exception exception, Location location = null)
        {
            var diagnostic = Diagnostic.Create(
                Descriptor,
                location ?? Location.None,
                generatorName,
                exception.ToString());

            context.ReportDiagnostic(diagnostic);
        }
        public static void ReportException(SourceProductionContext context, string generatorName, Exception exception, Location location = null)
        {
            var diagnostic = Diagnostic.Create(
                Descriptor,
                location ?? Location.None, 
                generatorName, 
                exception.ToString());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
