using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Infrastructure.Tests
{
    /// <summary>
    /// The account key is the lowercased address, and <see cref="ApplicationDbContext{TUser}"/> is
    /// what makes that true of every tracked write — including a consumer's own
    /// <c>DbSet&lt;TUser&gt;().Add(...)</c>, which no generated service can see. Pinned here rather
    /// than at the auth boundary because that boundary is exactly what these writes bypass.
    /// <para>
    /// Deliberately narrow: <see cref="Spiderly.Security.Interfaces.IUser"/>'s <c>Email</c> only.
    /// An address on any other entity is contact data the consumer's operator typed, and rewriting
    /// it would be the framework editing input it does not own — see
    /// <see cref="An_address_on_a_non_user_entity_is_left_alone"/>, which is the guard on that line.
    /// </para>
    /// </summary>
    public class AccountKeyCanonicalizationTests
    {
        [Fact]
        public async Task Insert_stores_the_account_key_canonically()
        {
            using SqliteConnection connection = NewOpenConnection();

            long id;
            using (TestDbContext ctx = NewContext(connection))
            {
                TestUser user = new() { Email = "  Kupac@Example.COM  " };
                ctx.Set<TestUser>().Add(user);
                await ctx.SaveChangesAsync();
                id = user.Id;
            }

            using (TestDbContext ctx = NewContext(connection))
            {
                TestUser reloaded = await ctx.Set<TestUser>().SingleAsync(x => x.Id == id);
                Assert.Equal("kupac@example.com", reloaded.Email);
            }
        }

        [Fact]
        public async Task Update_stores_the_account_key_canonically()
        {
            using SqliteConnection connection = NewOpenConnection();

            long id;
            using (TestDbContext ctx = NewContext(connection))
            {
                TestUser user = new() { Email = "stari@example.com" };
                ctx.Set<TestUser>().Add(user);
                await ctx.SaveChangesAsync();
                id = user.Id;
            }

            using (TestDbContext ctx = NewContext(connection))
            {
                TestUser user = await ctx.Set<TestUser>().SingleAsync(x => x.Id == id);
                user.Email = "Novi@Example.com";
                await ctx.SaveChangesAsync();
            }

            using (TestDbContext ctx = NewContext(connection))
            {
                TestUser reloaded = await ctx.Set<TestUser>().SingleAsync(x => x.Id == id);
                Assert.Equal("novi@example.com", reloaded.Email);
            }
        }

        // The line between an account key and contact data. Without this the obvious next step is to
        // fold every property that looks like an address, which would have the framework rewriting a
        // warehouse's or a warranty contact's e-mail — operator input it does not own.
        [Fact]
        public async Task An_address_on_a_non_user_entity_is_left_alone()
        {
            using SqliteConnection connection = NewOpenConnection();

            long id;
            using (TestDbContext ctx = NewContext(connection))
            {
                ContactEntity contact = new() { Email = "Magacin@Example.com" };
                ctx.Set<ContactEntity>().Add(contact);
                await ctx.SaveChangesAsync();
                id = contact.Id;
            }

            using (TestDbContext ctx = NewContext(connection))
            {
                ContactEntity reloaded = await ctx.Set<ContactEntity>().SingleAsync(x => x.Id == id);
                Assert.Equal("Magacin@Example.com", reloaded.Email);
            }
        }

        // The write stamp above cannot reach the synchronous SaveChanges (Spiderly does not override
        // it) or raw SQL, so the guarantee is only as good as its backstop. UNIQUE(Email) over values
        // this constraint forces canonical IS case-insensitive uniqueness — which is what makes the
        // promise identical on Postgres and SQL Server, whose default collations disagree.
        [Fact]
        public void The_user_table_constrains_the_account_key_to_its_canonical_form()
        {
            using ModelOnlyDbContext ctx = NewModelOnlyContext();

            // Check constraints live in the design-time model only — the runtime model drops them.
            IModel model = ctx.GetService<IDesignTimeModel>().Model;
            IEntityType user = Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(typeof(TestUser)));
            ICheckConstraint constraint = Assert.Single(user.GetCheckConstraints());

            Assert.Equal("CK_TestUser_Email_Lowercase", constraint.Name);
            Assert.Equal("\"Email\" = LOWER(\"Email\")", constraint.Sql);
        }

        #region Harness

        private static SqliteConnection NewOpenConnection()
        {
            SqliteConnection connection = new("DataSource=:memory:");
            connection.Open(); // the in-memory database lives only while a connection is open
            return connection;
        }

        private static TestDbContext NewContext(SqliteConnection connection)
        {
            TestDbContext ctx = new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        /// <summary>
        /// Runs the REAL <see cref="ApplicationDbContext{TUser}.OnModelCreating"/> — the constraint is
        /// added there, so a fixture that bypasses it (as the behavioural ones above do) cannot see it.
        /// Npgsql because the constraint is provider-quoted; no connection is ever opened.
        /// </summary>
        private static ModelOnlyDbContext NewModelOnlyContext() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=model-only.invalid;Database=spiderly_model_only;Username=none;Password=none")
                .Options);

        private sealed class ModelOnlyDbContext : ApplicationDbContext<TestUser>
        {
            public ModelOnlyDbContext(DbContextOptions options) : base(options) { }
        }

        /// <summary>An address that is contact data rather than an identity.</summary>
        private class ContactEntity : BusinessObject<long>
        {
            public string Email { get; set; } = null!;
        }

        private sealed class TestDbContext : ApplicationDbContext<TestUser>
        {
            public TestDbContext(DbContextOptions options) : base(options) { }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                // Map only what these tests need — the base's assembly-wide entity discovery and
                // relationship passes need a full consumer model we don't have here.
                modelBuilder.Entity<TestUser>();
                modelBuilder.Entity<ContactEntity>();
            }
        }

        #endregion
    }
}
