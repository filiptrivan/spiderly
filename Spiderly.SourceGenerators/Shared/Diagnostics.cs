using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Diagnostics;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class Diagnostics
    {
        private static readonly DiagnosticDescriptor ExceptionDescriptor = new DiagnosticDescriptor(
            "SPG001",
            "Source Generator Exception",
            "Exception in {0}: {1} at {2}:{3} in method {4}. Stack trace: {5}",
            "SourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor DebugStackTraceDescriptor = new DiagnosticDescriptor(
            "SPG003",
            "Debug Stack Trace Info",
            "Debug Stack Trace Analysis: {0}",
            "SourceGenerator",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        /// <summary>
        /// Debug method to analyze what's actually in the stack trace
        /// </summary>
        public static void ReportDebugStackTrace(SourceProductionContext context, string generatorName, Exception exception)
        {
            var stackTrace = new StackTrace(exception, true);
            var frames = stackTrace.GetFrames();

            if (frames == null || frames.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(DebugStackTraceDescriptor, Location.None, "No frames available"));
                return;
            }

            var debugInfo = string.Join("\n", frames.Select((f, i) =>
                $"Frame {i}: Method={f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}(), " +
                $"File={f.GetFileName() ?? "NULL"}, " +
                $"Line={f.GetFileLineNumber()}, " +
                $"HasFileInfo={f.GetFileName() != null}"));

            context.ReportDiagnostic(Diagnostic.Create(DebugStackTraceDescriptor, Location.None, debugInfo));
        }

        private static readonly DiagnosticDescriptor SyntaxExceptionDescriptor = new DiagnosticDescriptor(
            "SPG002",
            "Source Generator Syntax Exception",
            "Exception in {0}: {1}. Stack trace: {2}",
            "SourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static void ReportException(SourceProductionContext context, string generatorName, Exception exception)
        {
            var stackTrace = new StackTrace(exception, true);
            var frames = stackTrace.GetFrames();

            // Find the most relevant frame - prioritize SourceGenerators files
            var relevantFrame = frames?.FirstOrDefault(f =>
                f.GetFileName() != null &&
                f.GetFileName().Contains("SourceGenerators"));

            // If no SourceGenerators frame found, get any non-system frame
            if (relevantFrame == null)
            {
                relevantFrame = frames?.FirstOrDefault(f =>
                    f.GetFileName() != null &&
                    !f.GetFileName().Contains("System.") &&
                    !f.GetFileName().Contains("Microsoft."));
            }

            // If still no frame found, get any frame with file info
            if (relevantFrame == null)
            {
                relevantFrame = frames?.FirstOrDefault(f => f.GetFileName() != null);
            }

            string fileName = relevantFrame?.GetFileName() ?? "Unknown";
            int lineNumber = relevantFrame?.GetFileLineNumber() ?? 0;
            string methodName = relevantFrame?.GetMethod()?.Name ?? "Unknown";
            string shortFileName = System.IO.Path.GetFileName(fileName);

            // Get the full method signature for better context
            string fullMethodName = (relevantFrame?.GetMethod()?.DeclaringType?.Name ?? "Unknown") + "." + methodName;

            // Create detailed stack trace info
            string detailedStackInfo = GetDetailedStackInfo(frames);

            // If we don't have good file info, fall back to the raw exception stack trace
            if (fileName == "Unknown" || lineNumber == 0)
            {
                detailedStackInfo = $"Raw Stack Trace:\n{exception.StackTrace}\n\nParsed: {detailedStackInfo}";
            }

            // Also include the full exception details
            string fullExceptionInfo = $"Exception: {exception.GetType().Name}\nMessage: {exception.Message}\nInner Exception: {exception.InnerException?.Message ?? "None"}";

            var diagnostic = Diagnostic.Create(
                ExceptionDescriptor,
                Location.None,
                generatorName,
                exception.Message,
                shortFileName,
                lineNumber,
                fullMethodName,
                $"{fullExceptionInfo}\n\n{detailedStackInfo}");

            context.ReportDiagnostic(diagnostic);

            // Also report debug info to help diagnose the issue
            ReportDebugStackTrace(context, generatorName, exception);
        }

        /// <summary>
        /// Reports exception with red squiggly line at specific syntax node
        /// </summary>
        public static void ReportExceptionAtSyntax(SourceProductionContext context, string generatorName, Exception exception, SyntaxNode syntaxNode)
        {
            var stackTrace = new StackTrace(exception, true);
            var frames = stackTrace.GetFrames();
            string detailedStackInfo = GetDetailedStackInfo(frames);

            var location = syntaxNode?.GetLocation() ?? Location.None;

            var diagnostic = Diagnostic.Create(
                SyntaxExceptionDescriptor,
                location,
                generatorName,
                exception.Message,
                detailedStackInfo);

            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Reports exception with red squiggly line at specific class declaration
        /// </summary>
        public static void ReportExceptionAtClass(SourceProductionContext context, string generatorName, Exception exception, ClassDeclarationSyntax classDeclaration)
        {
            ReportExceptionAtSyntax(context, generatorName, exception, classDeclaration);
        }

        /// <summary>
        /// Reports exception with red squiggly line at specific property
        /// </summary>
        public static void ReportExceptionAtProperty(SourceProductionContext context, string generatorName, Exception exception, PropertyDeclarationSyntax propertyDeclaration)
        {
            ReportExceptionAtSyntax(context, generatorName, exception, propertyDeclaration);
        }

        /// <summary>
        /// Reports exception with red squiggly line at specific class identifier only
        /// </summary>
        public static void ReportExceptionAtClassIdentifier(SourceProductionContext context, string generatorName, Exception exception, ClassDeclarationSyntax classDeclaration)
        {
            var stackTrace = new StackTrace(exception, true);
            var frames = stackTrace.GetFrames();
            string detailedStackInfo = GetDetailedStackInfo(frames);

            var location = classDeclaration?.Identifier.GetLocation() ?? Location.None;

            var diagnostic = Diagnostic.Create(
                SyntaxExceptionDescriptor,
                location,
                generatorName,
                exception.Message,
                detailedStackInfo);

            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Reports exception with red squiggly line at specific property identifier only
        /// </summary>
        public static void ReportExceptionAtPropertyIdentifier(SourceProductionContext context, string generatorName, Exception exception, PropertyDeclarationSyntax propertyDeclaration)
        {
            var stackTrace = new StackTrace(exception, true);
            var frames = stackTrace.GetFrames();
            string detailedStackInfo = GetDetailedStackInfo(frames);

            var location = propertyDeclaration?.Identifier.GetLocation() ?? Location.None;

            var diagnostic = Diagnostic.Create(
                SyntaxExceptionDescriptor,
                location,
                generatorName,
                exception.Message,
                detailedStackInfo);

            context.ReportDiagnostic(diagnostic);
        }

        private static string GetDetailedStackInfo(StackFrame[] frames)
        {
            if (frames == null || frames.Length == 0)
                return "No stack trace available";

            // First try to get SourceGenerators frames
            var sourceGeneratorFrames = frames
                .Where(f => f.GetFileName() != null &&
                           f.GetFileName().Contains("SourceGenerators"))
                .Take(10)
                .ToList();

            if (sourceGeneratorFrames.Any())
            {
                return string.Join(" -> ", sourceGeneratorFrames.Select(f =>
                    $"{System.IO.Path.GetFileName(f.GetFileName())}:{f.GetFileLineNumber()} in {f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}()"));
            }

            // If no SourceGenerators frames, get any non-system frames
            var relevantFrames = frames
                .Where(f => f.GetFileName() != null &&
                           !f.GetFileName().Contains("System.") &&
                           !f.GetFileName().Contains("Microsoft."))
                .Take(10)
                .ToList();

            if (relevantFrames.Any())
            {
                return string.Join(" -> ", relevantFrames.Select(f =>
                    $"{System.IO.Path.GetFileName(f.GetFileName())}:{f.GetFileLineNumber()} in {f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}()"));
            }

            // If still no relevant frames, show all frames with file info
            var framesWithFiles = frames
                .Where(f => f.GetFileName() != null)
                .Take(10)
                .ToList();

            if (framesWithFiles.Any())
            {
                return string.Join(" -> ", framesWithFiles.Select(f =>
                    $"{System.IO.Path.GetFileName(f.GetFileName())}:{f.GetFileLineNumber()} in {f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}()"));
            }

            // Last resort - show method names only
            return string.Join(" -> ", frames.Take(10).Select(f =>
                $"{f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}()"));
        }
    }
}