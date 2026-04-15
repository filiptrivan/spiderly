using Microsoft.CodeAnalysis;
using System;

namespace Spiderly.SourceGenerators.Shared
{
    public static class GeneratorRunner
    {
        /// <summary>
        /// Same as <see cref="IncrementalGeneratorInitializationContext.RegisterImplementationSourceOutput"/>
        /// but reports <see cref="SpiderlyGenerationException.Diagnostic"/> instead of letting the generator fault with CS8785.
        /// </summary>
        public static void RegisterSafeImplementationSourceOutput<TSource>(
            this IncrementalGeneratorInitializationContext context,
            IncrementalValueProvider<TSource> source,
            Action<SourceProductionContext, TSource> body)
        {
            context.RegisterImplementationSourceOutput(source, (spc, s) =>
            {
                try
                {
                    body(spc, s);
                }
                catch (SpiderlyGenerationException ex)
                {
                    spc.ReportDiagnostic(ex.Diagnostic);
                }
            });
        }
    }
}
