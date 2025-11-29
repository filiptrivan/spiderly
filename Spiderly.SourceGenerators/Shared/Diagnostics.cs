using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class SpiderlyDescriptors
    {
        public static readonly DiagnosticDescriptor UnhadledGeneratorException = new(
            id: "SPDR0001",
            title: "Unhandled Exception in Source Gnerator",
            messageFormat: "Exception in '{0}': '{1}'",
            category: "Spiderly.Generators",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor GeneratorExceptionAtClass = new(
            id: "SPDR0002",
            title: "Error in Class during Generation",
            messageFormat: "Exception while processing class '{0}' : {1}",
            category: "Spiderly.Generators",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor MissingRequiredAttribute = new(
            id: "SPDR0003",
            title: "Missing Required Attribute",
            messageFormat: "The requred attribute '{0}' is missing on class or property '{1}'.",
            category: "Spiderly.Validation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor InvalidAttributeUsage = new(
            id: "SPDR0004",
            title: "Invalid Attribute Usage",
            messageFormat: "The attribute '{0}' is misused on '{1}'. Reason: {2}",
            category: "Spiderly.Validation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor InvalidPropertyType = new(
            id: "SPDR0005",
            title: "Invalid Property Type",
            messageFormat: "Property '{0}' has an unsupported type '{1}' for generator '{2}'.",
            category: "Spiderly.Validation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }

    public static class Diagnostics
    {
        public static void ReportException(
            SourceProductionContext context,
            string generatorName,
            Exception exception,
            IEnumerable<ClassDeclarationSyntax> classDeclarations = null
        )
        {
            string fullMessage = exception.Message + "\n\n" + exception.StackTrace;

            if (classDeclarations != null)
            {
                foreach (var classDecl in classDeclarations)
                {
                    var diagnostic = Diagnostic.Create(
                        SpiderlyDescriptors.GeneratorExceptionAtClass,
                        classDecl.Identifier.GetLocation(),
                        classDecl.Identifier.Text,
                        fullMessage
                    );

                    context.ReportDiagnostic(diagnostic);
                }
            }

            var fallback = Diagnostic.Create(
                SpiderlyDescriptors.UnhadledGeneratorException,
                Location.None,
                generatorName,
                fullMessage
            );

            context.ReportDiagnostic(fallback);
        }

        public static void ReportMissingRequiredAttribute(
            SourceProductionContext context,
            Location location,
            string attributeName,
            string targetName
         )
        {
            var diagnostic = Diagnostic.Create(
                SpiderlyDescriptors.MissingRequiredAttribute,
                location,
                attributeName,
                targetName
            );
            context.ReportDiagnostic(diagnostic);
        }

        public static void ReportInvalidAttributeUsage(
            SourceProductionContext context,
            Location location,
            string attributeName,
            string targetName,
            string reason
        )
        {
            var diagnostic = Diagnostic.Create(
                SpiderlyDescriptors.InvalidAttributeUsage,
                location,
                attributeName,
                targetName,
                reason
            );
            context.ReportDiagnostic(diagnostic);
        }

        public static void ReportInvalidPropertyType(
            SourceProductionContext context,
            Location location,
            string propertyName,
            string typeName,
            string generatorName
        )
        {
            var diagnostic = Diagnostic.Create(
                SpiderlyDescriptors.InvalidPropertyType,
                location,
                propertyName,
                typeName,
                generatorName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }
}