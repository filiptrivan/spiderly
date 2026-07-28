using System;
using Spiderly.Shared.Authorization;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Spiderly.Shared.Contracts;

namespace Spiderly.Shared.Exceptions
{
    /// <summary>
    /// Single source of truth for how Spiderly classifies an exception: the log level
    /// <see cref="SpiderlyExceptionHandler"/> records it at, and — derived from that — whether it is an
    /// <em>expected</em>, client-facing condition that an error tracker (Sentry, etc.) should ignore.
    /// The handler consumes <see cref="GetLogLevel"/>, so its logging and this classification can never drift;
    /// error-tracker integrations consume <see cref="IsExpected"/>.
    /// </summary>
    public static class SpiderlyExceptionClassifier
    {
        /// <summary>
        /// The level at which <see cref="SpiderlyExceptionHandler"/> logs the given exception. Expected,
        /// client-facing conditions (mapped to 4xx) are <see cref="LogLevel.Information"/> or
        /// <see cref="LogLevel.Warning"/>; genuine server faults (500) and <see cref="SecurityViolationException"/>
        /// are <see cref="LogLevel.Error"/>. Pure — depends only on <paramref name="exception"/>.
        /// </summary>
        /// <example>
        /// logLevel = SpiderlyExceptionClassifier.GetLogLevel(ex);
        /// </example>
        public static LogLevel GetLogLevel(Exception exception) => exception switch
        {
            BusinessException => LogLevel.Information,
            SpiderlyValidationException => LogLevel.Information,
            ValidationException => LogLevel.Information,
            ExpiredVerificationException => LogLevel.Information,
            SecurityTokenException => LogLevel.Information,
            UnauthorizedException => LogLevel.Warning,
            // A machine principal on a human-only path is an authorization refusal, not a server fault. It
            // derives from InvalidOperationException, so without this arm it would fall through to Error and
            // page someone every time a partner key touches an identity-scoped endpoint.
            PrincipalKindMismatchException => LogLevel.Warning,
            SecurityViolationException => LogLevel.Error,
            // DbUpdateConcurrencyException derives from DbUpdateException — match it FIRST so the
            // constraint check below doesn't (it has no constraint inner) drop it through to Error.
            DbUpdateConcurrencyException => LogLevel.Warning,
            DbUpdateException dbUpdate when GetDbConstraintErrorCode(dbUpdate) != null => LogLevel.Warning,
            _ => LogLevel.Error,
        };

        /// <summary>
        /// True when the exception is an expected, client-facing condition that an error tracker should NOT
        /// treat as a problem (it is handled into a 4xx and logged below <see cref="LogLevel.Error"/>).
        /// False for genuine server faults and <see cref="SecurityViolationException"/>, which are reportable.
        /// Wire this into an error-tracker filter (e.g. Sentry's <c>SetBeforeSend</c>) to suppress expected noise.
        /// </summary>
        /// <example>
        /// options.SetBeforeSend((e, _) =>
        ///     e.Exception != null &amp;&amp; SpiderlyExceptionClassifier.IsExpected(e.Exception) ? null : e);
        /// </example>
        public static bool IsExpected(Exception exception) => GetLogLevel(exception) < LogLevel.Error;

        /// <summary>
        /// Maps a <see cref="DbUpdateException"/> to the matching <see cref="ApiErrorCodes"/> when its inner
        /// exception is a recognized unique or foreign-key constraint violation (PostgreSQL or SQL Server);
        /// returns <c>null</c> for any other database failure (a genuine 500). The single detector used both to
        /// classify the log level above and to build the 409 response body in <see cref="SpiderlyExceptionHandler"/>.
        /// </summary>
        public static string GetDbConstraintErrorCode(DbUpdateException exception)
        {
            // SQL Server ships no named-constant class for these (unlike Npgsql's PostgresErrorCodes), so keep the ints.
            const int sqlServerUniqueConstraint = 2627;
            const int sqlServerUniqueIndex = 2601;
            const int sqlServerForeignKey = 547;

            if (exception.InnerException is PostgresException pg)
            {
                switch (pg.SqlState)
                {
                    case PostgresErrorCodes.UniqueViolation:
                        return ApiErrorCodes.UniqueViolation;
                    case PostgresErrorCodes.ForeignKeyViolation:
                        return ApiErrorCodes.ForeignKeyViolation;
                }
            }

            if (exception.InnerException is SqlException sql)
            {
                switch (sql.Number)
                {
                    case sqlServerUniqueConstraint:
                    case sqlServerUniqueIndex:
                        return ApiErrorCodes.UniqueViolation;
                    case sqlServerForeignKey:
                        return ApiErrorCodes.ForeignKeyViolation;
                }
            }

            return null;
        }
    }
}
