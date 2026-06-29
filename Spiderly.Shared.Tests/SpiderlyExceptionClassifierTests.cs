using System;
using System.Collections.Generic;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Spiderly.Shared.Exceptions;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins the single source of truth for exception classification. <see cref="SpiderlyExceptionHandler"/>
    /// consumes <see cref="SpiderlyExceptionClassifier.GetLogLevel"/>, and error-tracker integrations
    /// (e.g. a consumer's Sentry <c>beforeSend</c>) consume <see cref="SpiderlyExceptionClassifier.IsExpected"/>,
    /// so the "what reaches the error tracker" rule can't drift from the handler's logging.
    /// </summary>
    public class SpiderlyExceptionClassifierTests
    {
        public static IEnumerable<object[]> ExpectedExceptions => new List<object[]>
        {
            new object[] { new BusinessException("Out of stock"), LogLevel.Information },
            new object[] { new SpiderlyValidationException(new Dictionary<string, string[]>()), LogLevel.Information },
            new object[] { new ValidationException("Validation failed"), LogLevel.Information },
            new object[] { new ExpiredVerificationException(), LogLevel.Information },
            new object[] { new SecurityTokenException("Token expired"), LogLevel.Information },
            new object[] { new UnauthorizedException(), LogLevel.Warning },
            new object[] { new DbUpdateConcurrencyException(), LogLevel.Warning },
            new object[] { DbUpdate("23505"), LogLevel.Warning }, // unique_violation
            new object[] { DbUpdate("23503"), LogLevel.Warning }, // foreign_key_violation
        };

        public static IEnumerable<object[]> ReportableExceptions => new List<object[]>
        {
            new object[] { new SecurityViolationException("File content mismatch") },
            new object[] { new InvalidOperationException("Synchronous operations are disallowed") },
            new object[] { DbUpdate("42P01") }, // undefined_table — a real bug, not a constraint
            new object[] { new DbUpdateException("fail", new InvalidOperationException("boom")) },
        };

        [Theory]
        [MemberData(nameof(ExpectedExceptions))]
        public void Expected_exceptions_log_below_error_and_are_not_reported(Exception exception, LogLevel expectedLevel)
        {
            Assert.Equal(expectedLevel, SpiderlyExceptionClassifier.GetLogLevel(exception));
            Assert.True(SpiderlyExceptionClassifier.IsExpected(exception));
        }

        [Theory]
        [MemberData(nameof(ReportableExceptions))]
        public void Reportable_exceptions_log_at_error_and_are_reported(Exception exception)
        {
            Assert.Equal(LogLevel.Error, SpiderlyExceptionClassifier.GetLogLevel(exception));
            Assert.False(SpiderlyExceptionClassifier.IsExpected(exception));
        }

        private static DbUpdateException DbUpdate(string sqlState) =>
            new("update failed", new PostgresException("dup", "ERROR", "ERROR", sqlState));
    }
}
