using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spiderly.Security.Authentication;
using Spiderly.Security.Extensions;
using Spiderly.Security.Interfaces;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Pins the composed <see cref="AuthenticationOptions"/> defaults for an app that enables API-key auth:
    /// all three default schemes must resolve to the forwarding <see cref="ApiKeyAuthenticationDefaults.PolicyScheme"/>,
    /// <b>regardless of registration order</b> relative to AddSpiderly's mandatory JWT registration. The API-key
    /// extension opts in inside the AddSpiderly builder lambda, while the JWT registration runs after the lambda and
    /// sets <c>DefaultScheme = JwtBearer</c> via its own <c>Configure&lt;AuthenticationOptions&gt;</c>. If the extension
    /// used <c>Configure</c> (not <c>PostConfigure</c>), JWT's later Configure would overwrite <c>DefaultScheme</c> and
    /// this test's api-key-first case would fail — quietly bypassing the forwarding scheme for anything that falls back
    /// to the default (SignIn/SignOut/Forbid, or a future change to the explicit authenticate/challenge assignments).
    /// </summary>
    public class ApiKeySchemeDefaultsTests
    {
        [Theory]
        // api-key-first mirrors the real order (AddApiKeys runs inside the AddSpiderly lambda, JWT after it); this is
        // the case that regresses to DefaultScheme = JwtBearer if the extension uses Configure instead of PostConfigure.
        [InlineData(true)]
        [InlineData(false)]
        public void ForwardingPolicyScheme_IsEveryDefault_RegardlessOfRegistrationOrder(bool apiKeyFirst)
        {
            ServiceCollection services = new();
            services.AddLogging();

            if (apiKeyFirst)
            {
                services.AddSpiderlyApiKeyAuthentication<TestApiKey>();
                // Stands in for SpiderlyAddAuthentication: AddAuthentication(scheme) is what registers the
                // Configure<AuthenticationOptions> that sets DefaultScheme = JwtBearer. The .AddJwtBearer() that
                // follows in production only touches JwtBearerOptions, not the AuthenticationOptions defaults.
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
            }
            else
            {
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
                services.AddSpiderlyApiKeyAuthentication<TestApiKey>();
            }

            AuthenticationOptions options = services.BuildServiceProvider()
                .GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            Assert.Equal(ApiKeyAuthenticationDefaults.PolicyScheme, options.DefaultScheme);
            Assert.Equal(ApiKeyAuthenticationDefaults.PolicyScheme, options.DefaultAuthenticateScheme);
            Assert.Equal(ApiKeyAuthenticationDefaults.PolicyScheme, options.DefaultChallengeScheme);
        }
    }

    /// <summary>
    /// Minimal <see cref="IApiKey"/> so the generic <c>AddSpiderlyApiKeyAuthentication&lt;TApiKey&gt;</c> constraint is
    /// satisfied. Only the type is needed — the authenticator over it is registered, never resolved, in these tests.
    /// </summary>
    file sealed class TestApiKey : IApiKey
    {
        public string KeyHash { get; set; } = null!;
        public bool? IsRevoked { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool? IsDisabled { get; set; }
        public IReadOnlyCollection<IRole> Roles => Array.Empty<IRole>();
        public long Id { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}
