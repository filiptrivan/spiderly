using Microsoft.Extensions.Logging;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Exceptions
{
    /// <summary>
    /// Fans an unhandled exception out to every registered <see cref="IExceptionReporter"/>, isolating each so a
    /// single failing reporter can't break the error response or starve the others.
    /// </summary>
    public static class ExceptionReporting
    {
        public static void ReportAll(IEnumerable<IExceptionReporter> reporters, ExceptionReport report, ILogger logger)
        {
            foreach (IExceptionReporter reporter in reporters)
            {
                try
                {
                    reporter.Report(report);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception reporter {Reporter} threw while reporting an unhandled exception.", reporter.GetType().Name);
                }
            }
        }
    }
}
