using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Localization;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// The refresh path is the only authenticated flow that never reads the user row: <c>RefreshAsync</c>
    /// validates the refresh token and mints a new access token from its user id alone. Anything that
    /// invalidates a user between issuance and refresh — a deleted row, an admin disabling the account —
    /// is therefore invisible to it, and the session survives until the refresh token's own expiry.
    /// These tests pin that refresh re-checks the user and kills the session when it can't.
    /// </summary>
    public class RefreshTokenUserValidationTests
    {
        private const long UserId = 42;
        private const string Email = "user@example.com";
        private const string RefreshTokenString = "refresh-token-string";

        [Fact]
        public async Task RefreshToken_WhenUserRowWasDeleted_RejectsInsteadOfIssuingTokens()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            // No user seeded — the row was deleted while its refresh token was still live.
            StubJwtAuthManager jwtAuthManager = new();
            SecurityServiceBase<TestUser, TestUserExternalLogin> sut = NewSut(context, jwtAuthManager);

            await Assert.ThrowsAsync<SecurityTokenException>(() => sut.RefreshToken(NewRequest(), accessToken: null));
        }

        [Fact]
        public async Task RefreshToken_WhenUserRowWasDeleted_RevokesTheRefreshToken()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            StubJwtAuthManager jwtAuthManager = new();
            SecurityServiceBase<TestUser, TestUserExternalLogin> sut = NewSut(context, jwtAuthManager);

            await Assert.ThrowsAnyAsync<Exception>(() => sut.RefreshToken(NewRequest(), accessToken: null));

            // Rejecting once is not enough — the token stays valid and the client simply retries. The
            // session has to actually die.
            Assert.Equal(new[] { UserId }, jwtAuthManager.RevokedUserIds);
        }

        [Fact]
        public async Task RefreshToken_WhenUserIsDisabled_RejectsInsteadOfIssuingTokens()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            await SeedUserAsync(context, isDisabled: true);
            StubJwtAuthManager jwtAuthManager = new();
            SecurityServiceBase<TestUser, TestUserExternalLogin> sut = NewSut(context, jwtAuthManager);

            // Login (Authenticate) and external login (ResolveExternalUser) both reject a disabled user;
            // refresh must too, or disabling an account leaves it usable for a full refresh-token lifetime.
            await Assert.ThrowsAsync<SecurityTokenException>(() => sut.RefreshToken(NewRequest(), accessToken: null));
        }

        [Fact]
        public async Task RefreshToken_WhenUserIsActive_ReturnsTheirEmail()
        {
            using SqliteConnection connection = NewOpenConnection();
            using TestDbContext context = NewContext(connection);
            await SeedUserAsync(context, isDisabled: false);
            StubJwtAuthManager jwtAuthManager = new();
            SecurityServiceBase<TestUser, TestUserExternalLogin> sut = NewSut(context, jwtAuthManager);

            AuthResultDTO result = await sut.RefreshToken(NewRequest(), accessToken: null);

            Assert.Equal(Email, result.Email);
            Assert.Equal(UserId, result.UserId);
            Assert.Empty(jwtAuthManager.RevokedUserIds);
        }

        #region Harness

        private static RefreshTokenRequestDTO NewRequest() => new() { RefreshToken = RefreshTokenString };

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

        private static async Task SeedUserAsync(TestDbContext context, bool isDisabled)
        {
            context.Users.Add(new TestUser { Id = UserId, Email = Email, IsDisabled = isDisabled });
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Only the dependencies <c>RefreshToken</c> actually touches are real; the rest of the
        /// constructor's surface is not on this path.
        /// </summary>
        private static SecurityServiceBase<TestUser, TestUserExternalLogin> NewSut(
            TestDbContext context, IJwtAuthManager jwtAuthManager)
            => new(
                context,
                jwtAuthManager,
                emailingService: null!,
                authenticationService: null!,
                environment: null!,
                new PassthroughStringLocalizer(),
                Options.Create(new AuthPolicyOptions()),
                externalAuthProviderRegistry: null!,
                externalAuthCodeFlow: null!,
                new StubDataProtectionProvider(),
                Options.Create(new Shared.Settings()));

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
        }

        /// <summary>
        /// Stands in for the token layer: the refresh token itself is valid (that is not what these tests
        /// are about), so <c>RefreshAsync</c> succeeds and hands back a result for <see cref="UserId"/>.
        /// Records revocations so a test can assert the session was actually killed.
        /// </summary>
        private sealed class StubJwtAuthManager : IJwtAuthManager
        {
            public List<long> RevokedUserIds { get; } = new();

            public Task<JwtAuthResultDTO> RefreshAsync(RefreshTokenRequestDTO request, long? userIdFromAccessToken)
                => Task.FromResult(new JwtAuthResultDTO
                {
                    UserId = UserId,
                    AccessTokenDTO = new AccessTokenDTO { TokenString = "access", ExpiresAt = DateTime.UtcNow.AddMinutes(20) },
                    RefreshTokenDTO = new RefreshTokenDTO { UserId = UserId, TokenString = RefreshTokenString, ExpiresAt = DateTime.UtcNow.AddDays(1) },
                });

            public Task RemoveRefreshTokenByUserIdAsync(long userId)
            {
                RevokedUserIds.Add(userId);
                return Task.CompletedTask;
            }

            public Task<IImmutableDictionary<string, RefreshTokenDTO>> GetUsersRefreshTokensReadOnlyDictionaryAsync() => throw new NotSupportedException();
            public Task<IImmutableDictionary<string, LoginVerificationTokenDTO>> GetUsersLoginVerificationTokensReadOnlyDictionaryAsync() => throw new NotSupportedException();
            public Task<JwtAuthResultDTO> GenerateAccessAndRefreshTokensAsync(long userId, string? ipAddress, string? browserId) => throw new NotSupportedException();
            public List<Claim> GenerateClaims(long userId) => throw new NotSupportedException();
            public Task<List<Claim>> GetClaimsForTheAccessTokenAsync(RefreshTokenRequestDTO request, string accessToken) => throw new NotSupportedException();
            public Task RemoveExpiredRefreshTokensAsync() => throw new NotSupportedException();
            public Task LogoutAsync(string? browserId, long userId) => throw new NotSupportedException();
            public Task<bool> RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(string? browserId, long userId) => throw new NotSupportedException();
            public Task<LoginVerificationTokenDTO> ValidateAndGetLoginVerificationTokenDTOAsync(string verificationToken, string? browserId, string email) => throw new NotSupportedException();
            public Task<string> GenerateAndSaveLoginVerificationCodeAsync(string userEmail, string? browserId) => throw new NotSupportedException();
            public Task RemoveLoginVerificationTokensByEmailAsync(string email) => throw new NotSupportedException();
            public Task<bool> IsLoginVerificationSendBlockedAsync(string email) => throw new NotSupportedException();
        }

        /// <summary>Constructor-only dependency — the protectors are never exercised on the refresh path.</summary>
        private sealed class StubDataProtectionProvider : IDataProtectionProvider, IDataProtector
        {
            public IDataProtector CreateProtector(string purpose) => this;
            public byte[] Protect(byte[] plaintext) => plaintext;
            public byte[] Unprotect(byte[] protectedData) => protectedData;
        }

        #endregion
    }
}
