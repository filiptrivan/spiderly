using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Spiderly.Security.DTO;
using Spiderly.Security.Helpers;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Emailing;
using Spiderly.Shared.ExternalAuth;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Localization;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Pins that an address identifies ONE account whatever its casing — the rule and its rationale
    /// live on <see cref="EmailNormalizer"/>; what follows is why these tests can see a violation.
    /// <para>
    /// The store is SQLite rather than the EF in-memory provider: its TEXT columns use BINARY
    /// collation, so <c>==</c> is case-sensitive exactly as it is on Postgres. A provider that
    /// folded case would make every one of these pass against unfixed code.
    /// </para>
    /// <para>
    /// The token layer is the REAL <see cref="JwtAuthManagerService"/> over a real store, not a
    /// stub, so a fix that normalized the database lookup alone would fail
    /// <see cref="Login_AcceptsACodeThatWasRequestedUnderADifferentCasing"/> rather than pass.
    /// </para>
    /// </summary>
    public class LoginEmailCasingTests
    {
        private const long ExistingUserId = 42;
        private const string StoredEmail = "geripagram@gmail.com";
        private const string TypedEmail = "Geripagram@gmail.com";
        private const string BrowserId = "browser-1";
        private const string ValidKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Fact]
        public async Task Login_WithAnotherCasingOfAnExistingAddress_SignsIntoThatAccountInsteadOfCreatingASecond()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            SeedUser(context);
            Harness harness = NewHarness(context);

            await harness.Sut.SendLoginVerificationEmail(new LoginDTO { Email = TypedEmail, BrowserId = BrowserId });

            AuthResultDTO result = await harness.Sut.Login(new VerificationTokenRequestDTO
            {
                Email = TypedEmail,
                VerificationCode = await harness.ReadIssuedCodeAsync(),
                BrowserId = BrowserId,
            });

            Assert.Equal(ExistingUserId, result.UserId);
            Assert.Single(context.Users);
        }

        /// <summary>
        /// The two steps are separate requests, so nothing guarantees the address is spelled the
        /// same way twice — and this is the half a lookup-only fix breaks.
        /// </summary>
        [Fact]
        public async Task Login_AcceptsACodeThatWasRequestedUnderADifferentCasing()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            SeedUser(context);
            Harness harness = NewHarness(context);

            await harness.Sut.SendLoginVerificationEmail(new LoginDTO { Email = StoredEmail, BrowserId = BrowserId });

            AuthResultDTO result = await harness.Sut.Login(new VerificationTokenRequestDTO
            {
                Email = "GERIPAGRAM@GMAIL.COM",
                VerificationCode = await harness.ReadIssuedCodeAsync(),
                BrowserId = BrowserId,
            });

            Assert.Equal(ExistingUserId, result.UserId);
        }

        /// <summary>
        /// The provider asserts whatever casing it holds, which need not match what an earlier
        /// email-code sign-in stored. This is how the observed production split actually happened.
        /// </summary>
        [Fact]
        public async Task ExternalLogin_LinksToAnExistingAccountSpelledInAnotherCasing()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            SeedUser(context);
            Harness harness = NewHarness(context);

            TestUser resolved = await harness.Sut.ResolveExternalUserForTest(new ExternalIdentity
            {
                Provider = "google",
                Subject = "google-subject-1",
                Email = TypedEmail,
                EmailVerified = true,
            });

            Assert.Equal(ExistingUserId, resolved.Id);
            Assert.Single(context.Users);
        }

        #region Harness

        private static SqliteConnection NewOpenConnection()
        {
            SqliteConnection connection = new("DataSource=:memory:");
            connection.Open();
            return connection;
        }

        private static TestDbContext NewContext(SqliteConnection connection)
        {
            TestDbContext context = new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);
            context.Database.EnsureCreated();
            return context;
        }

        private static void SeedUser(TestDbContext context)
        {
            context.Users.Add(new TestUser { Id = ExistingUserId, Email = StoredEmail, IsDisabled = false });
            context.SaveChanges();
        }

        private sealed record Harness(TestableSecurityService Sut, IJwtAuthManager JwtAuthManager)
        {
            /// <summary>The code as the customer would read it out of the email: the store is keyed by it.</summary>
            public async Task<string> ReadIssuedCodeAsync() =>
                (await JwtAuthManager.GetUsersLoginVerificationTokensReadOnlyDictionaryAsync()).Single().Key;
        }

        private static Harness NewHarness(TestDbContext context)
        {
            JwtAuthManagerService jwtAuthManager = new(
                // Both stores carry the same indexes AddSpiderlyTokenStorage wires in production —
                // the refresh store's UserId index is read while issuing tokens, so an unindexed
                // one fails the login for a reason that has nothing to do with these tests.
                new InMemoryTokenStorage<RefreshTokenDTO>(
                    new Dictionary<string, Func<RefreshTokenDTO, string?>>
                    {
                        { RefreshTokenDTO.UserIdIndex, t => t.UserId.ToString() },
                    }),
                new InMemoryTokenStorage<LoginVerificationTokenDTO>(
                    new Dictionary<string, Func<LoginVerificationTokenDTO, string?>>
                    {
                        { LoginVerificationTokenDTO.EmailIndex, t => t.Email },
                    }),
                new PassthroughStringLocalizer(),
                Options.Create(new JwtOptions
                {
                    JwtKey = ValidKey,
                    JwtIssuer = "https://test-issuer",
                    JwtAudience = "https://test-audience",
                    ClockSkewMinutes = 0,
                }),
                Options.Create(new AuthPolicyOptions()));

            TestableSecurityService sut = new(
                context,
                jwtAuthManager,
                new StubEmailingService(),
                new StubAuthenticationService(context),
                new StubWebHostEnvironment(),
                new PassthroughStringLocalizer(),
                Options.Create(new AuthPolicyOptions()),
                new StubDataProtectionProvider(),
                Options.Create(new Shared.Settings()));

            return new Harness(sut, jwtAuthManager);
        }

        /// <summary>
        /// <c>ResolveExternalUser</c> is a protected extension point, and reaching it through
        /// <c>LoginExternal</c> would mean standing up a provider registry and a signed id token to
        /// assert one thing about account resolution. This exposes it directly instead.
        /// </summary>
        private sealed class TestableSecurityService(
            IApplicationDbContext context,
            IJwtAuthManager jwtAuthManager,
            IEmailingService emailingService,
            AuthenticationService authenticationService,
            IWebHostEnvironment environment,
            IStringLocalizer localizer,
            IOptions<AuthPolicyOptions> authPolicyOptions,
            IDataProtectionProvider dataProtectionProvider,
            IOptions<Shared.Settings> sharedSettings)
            : SecurityServiceBase<TestUser, TestUserExternalLogin>(
                context, jwtAuthManager, emailingService, authenticationService, environment,
                localizer, authPolicyOptions, externalAuthProviderRegistry: null!,
                externalAuthCodeFlow: null!, dataProtectionProvider, sharedSettings)
        {
            public Task<TestUser> ResolveExternalUserForTest(ExternalIdentity externalIdentity) =>
                ResolveExternalUser(externalIdentity);
        }

        private sealed class TestUser : IUser
        {
            public long Id { get; set; }
            public int Version { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
            public bool? IsDisabled { get; set; }
            public string Email { get; set; } = null!;

            [NotMapped]
            public IReadOnlyCollection<IRole> Roles => Array.Empty<IRole>();
        }

        private sealed class TestUserExternalLogin : IUserExternalLogin
        {
            public long UserId { get; set; }
            public string Provider { get; set; } = null!;
            public string ProviderKey { get; set; } = null!;
        }

        private sealed class TestDbContext : DbContext, IApplicationDbContext
        {
            public TestDbContext(DbContextOptions options) : base(options) { }

            public DbSet<TestUser> Users => Set<TestUser>();

            public DbSet<TEntity> DbSet<TEntity>() where TEntity : class => Set<TEntity>();

            // The link row carries no surrogate id, and (Provider, ProviderKey) is the key the
            // external-login lookup actually resolves on.
            protected override void OnModelCreating(ModelBuilder modelBuilder) =>
                modelBuilder.Entity<TestUserExternalLogin>().HasKey(x => new { x.Provider, x.ProviderKey });
        }

        /// <summary>
        /// Configured, so the login path takes the real send branch rather than the development
        /// shortcut that returns the code in the response.
        /// </summary>
        private sealed class StubEmailingService : IEmailingService
        {
            public bool IsConfigured() => true;

            public Task SendVerificationEmailAsync(string toEmail, EmailVerifyUIDTO template) => Task.CompletedTask;

            public Task SendEmailAsync(string recipient, string subject, string body, EmailSender? from = null, EmailSender? replyTo = null) => throw new NotSupportedException();
            public Task SendEmailAsync(string recipient, string subject, string body, IEnumerable<EmailAttachment> attachments, EmailSender? from = null, EmailSender? replyTo = null) => throw new NotSupportedException();
            public Task SendEmailAsync(List<string> recipients, string subject, string body) => throw new NotSupportedException();
            public Task SendEmailFromBackgroundJobAsync(string recipient, string subject, string body) => throw new NotSupportedException();
        }

        /// <summary>Only <c>GetIPAddress</c> is on this path, and it has no HTTP context to read.</summary>
        private sealed class StubAuthenticationService : AuthenticationService
        {
            public StubAuthenticationService(IApplicationDbContext context)
                : base(
                    httpContextAccessor: null!,
                    principalAccessor: null!,
                    context,
                    new PassthroughStringLocalizer(),
                    Options.Create(new AuthPolicyOptions()),
                    Options.Create(new TokenKeyOptions()),
                    cookieManager: null!,
                    principalIdentity: null!)
            {
            }

            public override string? GetIPAddress() => null;
        }

        private sealed class StubWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Production";
            public string ApplicationName { get; set; } = nameof(LoginEmailCasingTests);
            public string WebRootPath { get; set; } = string.Empty;
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        private sealed class StubDataProtectionProvider : IDataProtectionProvider, IDataProtector
        {
            public IDataProtector CreateProtector(string purpose) => this;
            public byte[] Protect(byte[] plaintext) => plaintext;
            public byte[] Unprotect(byte[] protectedData) => protectedData;
        }

        #endregion
    }
}
