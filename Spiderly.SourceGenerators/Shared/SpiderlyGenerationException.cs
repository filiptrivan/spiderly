using Microsoft.CodeAnalysis;
using System;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Structured return channel from generator helpers — replaces raw <see cref="Exception"/> throws
    /// so users see a located SPIDERLY### diagnostic instead of a CS8785 "generator failed" stack trace.
    /// </summary>
    public sealed class SpiderlyGenerationException : Exception
    {
        public Diagnostic Diagnostic { get; }

        public SpiderlyGenerationException(Diagnostic diagnostic)
            : base((diagnostic ?? throw new ArgumentNullException(nameof(diagnostic))).GetMessage())
        {
            Diagnostic = diagnostic;
        }
    }
}
