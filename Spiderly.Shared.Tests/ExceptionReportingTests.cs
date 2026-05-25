using Microsoft.Extensions.Logging.Abstractions;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="ExceptionReporting.ReportAll"/> — every registered reporter is invoked, and a
    /// throwing reporter is isolated (it neither stops the others nor propagates out).
    /// </summary>
    public class ExceptionReportingTests
    {
        [Fact]
        public void ReportAll_invokes_every_reporter_even_when_one_throws()
        {
            RecordingReporter first = new();
            ThrowingReporter throwing = new();
            RecordingReporter last = new();
            ExceptionReport report = new(new InvalidOperationException("boom"), 42);

            ExceptionReporting.ReportAll(new IExceptionReporter[] { first, throwing, last }, report, NullLogger.Instance);

            Assert.Same(report, first.Received);
            Assert.Same(report, last.Received); // reached despite the throwing reporter before it
        }

        [Fact]
        public void ReportAll_with_no_reporters_is_a_noop()
        {
            ExceptionReporting.ReportAll(
                Array.Empty<IExceptionReporter>(),
                new ExceptionReport(new Exception(), null),
                NullLogger.Instance);
        }

        private sealed class RecordingReporter : IExceptionReporter
        {
            public ExceptionReport? Received { get; private set; }
            public void Report(ExceptionReport report) => Received = report;
        }

        private sealed class ThrowingReporter : IExceptionReporter
        {
            public void Report(ExceptionReport report) => throw new InvalidOperationException("reporter failure");
        }
    }
}
