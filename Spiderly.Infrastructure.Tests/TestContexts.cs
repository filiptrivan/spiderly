using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Spiderly.Infrastructure.Tests
{
    /// <summary>
    /// The two ways this project stands up a real <see cref="ApplicationDbContext{TUser}"/>, shared
    /// for the same reason <see cref="TestUser"/> is: two identical private copies is how a fourth
    /// appears. The context SHAPES stay private to each test file — those are test-specific and
    /// correctly differ; only the plumbing lives here.
    /// </summary>
    internal static class TestContexts
    {
        /// <summary>
        /// A SQLite in-memory connection for behavioural tests. Its TEXT columns are BINARY-collated,
        /// so <c>==</c> is case-sensitive exactly as it is on Postgres.
        /// </summary>
        public static SqliteConnection NewOpenConnection()
        {
            SqliteConnection connection = new("DataSource=:memory:");
            connection.Open(); // the in-memory database lives only while a connection is open
            return connection;
        }

        /// <summary>
        /// Options for a model-only context: a real provider so provider-specific model decisions are
        /// the ones a consumer gets, against a host that does not exist because no connection is ever
        /// opened. Npgsql is the interesting provider for those decisions.
        /// </summary>
        public static DbContextOptions ModelOnlyNpgsqlOptions(bool proxies = false)
        {
            DbContextOptionsBuilder builder = new DbContextOptionsBuilder()
                .UseNpgsql("Host=model-only.invalid;Database=spiderly_model_only;Username=none;Password=none");

            if (proxies)
                builder.UseLazyLoadingProxies();

            return builder.Options;
        }
    }
}
