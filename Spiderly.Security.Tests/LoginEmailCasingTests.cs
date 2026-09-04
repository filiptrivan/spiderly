using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Spiderly.Security.DTO;
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
    /// An email address identifies an account, and identity is case-insensitive: nobody who typed
    /// <c>Kupac@Example.com</c> at signup believes they own something different from
    /// <c>kupac@example.com</c>. The login path used to disagree — every user lookup was an ordinal
    /// <c>==</c> and every user it created stored the address exactly as typed — so a second casing
    /// silently minted a SECOND account, splitting one person's orders, addresses and external
    /// logins across two rows with no way back. Observed in production on two real customers.
    ///
    /// The store is SQLite rather than the EF in-memory provider deliberately: its TEXT columns use
    /// BINARY collation, so <c>==</c> is case-sensitive exactly as it is on Postgres and SQL Server
    /// with a case-sensitive collation. A provider that folded case would make these tests pass
    /// against unfixed code.
    ///
    /// The token layer is the REAL <see cref="JwtAuthManagerService"/> over a real in-memory store,
    /// not a stub, because the two halves of this fix are only correct together: the verification
    /// code is stored under one address and validated against another, both compared ordinally, so
    /// normalizing the database lookup alone turns a silent duplicate account into a login that
    /// cannot complete at all.
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
        /// same way twice. This is the half a lookup-only fix breaks: normalize where the account is
        /// found but not where the code is stored, and the code is minted under the row's stored
        /// casing while the request carries the typed one — an ordinal compare away from an expired
        /// -code error on a login that is entirely legitimate.
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
        /// The provider asserts whatever casing it holds, and an account created through the
        /// email-code path holds whatever the customer typed — so the two disagree by default. This
        /// is how the production split actually happened: a Google sign-in supplied the lowercase
        /// form, the typed form later minted the second row.
        /// </summary>
        [Fact]
        public async Task ExternalLogin_LinksToAnExistingAccountSpelledInAnotherCasing()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            SeedUser(context);
            TestableSecurityService sut = NewTestableSut(context);

            TestUser resolved = await sut.ResolveExternalUserForTest(new ExternalIdentity
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

        private sealed record Harness(SecurityServiceBase<TestUser, TestUserExternalLogin> Sut, IJwtAuthManager JwtAuthManager)
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

            SecurityServiceBase<TestUser, TestUserExternalLogin> sut = new(
                context,
                jwtAuthManager,
                new StubEmailingService(),
                new StubAuthenticationService(context),
                new StubWebHostEnvironment(),
                new PassthroughStringLocalizer(),
                Options.Create(new AuthPolicyOptions()),
                externalAuthProviderRegistry: null!,
                externalAuthCodeFlow: null!,
                new StubDataProtectionProvider(),
                Options.Create(new Shared.Settings()));

            return new Harness(sut, jwtAuthManager);
        }

        /// <summary>
        /// <c>ResolveExternalUser</c> is a protected extension point, and reaching it through
        /// <c>LoginExternal</c> would mean standing up a provider registry and a signed id token to
        /// assert one thing about account resolution. This exposes it directly instead.
        /// </summary>
        private sealed class TestableSecurityService : SecurityServiceBase<TestUser, TestUserExternalLogin>
        {
            public TestableSecurityService(
                IApplicationDbContext context,
                IJwtAuthManager jwtAuthManager,
                IEmailingService emailingService,
                AuthenticationService authenticationService,
                IWebHostEnvironment environment,
                IStringLocalizer localizer,
                IOptions<AuthPolicyOptions> authPolicyOptions,
                IDataProtectionProvider dataProtectionProvider,
                IOptions<Shared.Settings> sharedSettings)
                : base(context, jwtAuthManager, emailingService, authenticationService, environment,
                    localizer, authPolicyOptions, externalAuthProviderRegistry: null!,
                    externalAuthCodeFlow: null!, dataProtectionProvider, sharedSettings)
            {
            }

            public Task<TestUser> ResolveExternalUserForTest(ExternalIdentity externalIdentity) =>
                ResolveExternalUser(externalIdentity);
        }

        private static TestableSecurityService NewTestableSut(TestDbContext context) => new(
            context,
            new StubJwtAuthManagerForExternal(),
            new StubEmailingService(),
            new StubAuthenticationService(context),
            new StubWebHostEnvironment(),
            new PassthroughStringLocalizer(),
            Options.Create(new AuthPolicyOptions()),
            new StubDataProtectionProvider(),
            Options.Create(new Shared.Settings()));

        /// <summary>No token is issued while resolving the account — that happens after.</summary>
        private sealed class StubJwtAuthManagerForExternal : IJwtAuthManager
        {
            public Task<IImmutableDictionary<string, RefreshTokenDTO>> GetUsersRefreshTokensReadOnlyDictionaryAsync() => throw new NotSupportedException();
            public Task<IImmutableDictionary<string, LoginVerificationTokenDTO>> GetUsersLoginVerificationTokensReadOnlyDictionaryAsync() => throw new NotSupportedException();
            public Task<JwtAuthResultDTO> GenerateAccessAndRefreshTokensAsync(long userId, string? ipAddress, string? browserId) => throw new NotSupportedException();
            public Task<JwtAuthResultDTO> RefreshAsync(RefreshTokenRequestDTO request, long? userIdFromAccessToken) => throw new NotSupportedException();
            public List<Claim> GenerateClaims(long userId) => throw new NotSupportedException();
            public Task<List<Claim>> GetClaimsForTheAccessTokenAsync(RefreshTokenRequestDTO request, string accessToken) => throw new NotSupportedException();
            public Task RemoveExpiredRefreshTokensAsync() => throw new NotSupportedException();
            public Task RemoveRefreshTokenByUserIdAsync(long userId) => throw new NotSupportedException();
            public Task LogoutAsync(string? browserId, long userId) => throw new NotSupportedException();
            public Task<bool> RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(string? browserId, long userId) => throw new NotSupportedException();
            public Task<LoginVerificationTokenDTO> ValidateAndGetLoginVerificationTokenDTOAsync(string verificationToken, string? browserId, string email) => throw new NotSupportedException();
            public Task<string> GenerateAndSaveLoginVerificationCodeAsync(string userEmail, string? browserId) => throw new NotSupportedException();
            public Task RemoveLoginVerificationTokensByEmailAsync(string email) => throw new NotSupportedException();
            public Task<bool> IsLoginVerificationSendBlockedAsync(string email) => throw new NotSupportedException();
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

            public DbSet<TestUserExternalLogin> ExternalLogins => Set<TestUserExternalLogin>();

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
