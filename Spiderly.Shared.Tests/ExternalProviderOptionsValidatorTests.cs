using Microsoft.Extensions.Options;
using Spiderly.Shared;
using Spiderly.Shared.ExternalAuth;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="ExternalProviderOptionsValidator"/> — the boot-time guard (wired via
    /// <c>ValidateOnStart</c>) that owns the invariants <see cref="ExternalAuthProviderRegistry"/>'s ctor used to
    /// throw lazily at first resolution. Asserts each invariant, that all problems aggregate into one boot failure,
    /// and that a custom provider is the escape hatch excusing a config entry from needing an authority/preset.
    /// </summary>
    public class ExternalProviderOptionsValidatorTests
    {
        [Fact]
        public void Passes_for_a_preset_provider_with_a_client_id()
        {
            // "google" resolves its authority from the preset table, so only a ClientId is required.
            ValidateOptionsResult result = Validate(Options(Config("google", clientId: "abc")));

            Assert.True(result.Succeeded);
        }

        [Fact]
        public void Passes_for_an_empty_provider_list()
        {
            Assert.True(Validate(Options()).Succeeded);
        }

        [Fact]
        public void Fails_and_names_the_code_when_client_id_is_missing()
        {
            ValidateOptionsResult result = Validate(Options(Config("google", clientId: null)));

            Assert.True(result.Failed);
            Assert.Contains(result.Failures, f => f.Contains("google") && f.Contains("ClientId"));
        }

        [Fact]
        public void Fails_when_no_authority_no_preset_and_no_custom_provider()
        {
            // "acme" has no preset and no explicit Authority, and no custom IExternalAuthProvider is registered for it.
            ValidateOptionsResult result = Validate(Options(Config("acme", clientId: "abc")));

            Assert.True(result.Failed);
            Assert.Contains(result.Failures, f => f.Contains("acme") && f.Contains("Authority"));
        }

        [Fact]
        public void Passes_when_a_custom_provider_supplies_the_code()
        {
            // A custom provider for "acme" shadows the generic validator, so the entry needs neither authority nor clientId.
            ValidateOptionsResult result = Validate(
                Options(Config("acme")),
                new FakeProvider("acme"));

            Assert.True(result.Succeeded);
        }

        [Fact]
        public void Fails_on_a_missing_code()
        {
            ValidateOptionsResult result = Validate(Options(Config(code: " ", clientId: "abc")));

            Assert.True(result.Failed);
            Assert.Contains(result.Failures, f => f.Contains("missing a 'Code'"));
        }

        [Fact]
        public void Fails_on_duplicate_config_codes_case_insensitively()
        {
            ValidateOptionsResult result = Validate(Options(
                Config("google", clientId: "a"),
                Config("GOOGLE", clientId: "b")));

            Assert.True(result.Failed);
            Assert.Contains(result.Failures, f => f.Contains("duplicate"));
        }

        [Fact]
        public void Fails_when_a_custom_provider_returns_an_empty_code()
        {
            ValidateOptionsResult result = Validate(Options(), new FakeProvider(""));

            Assert.True(result.Failed);
            Assert.Contains(result.Failures, f => f.Contains("empty Code"));
        }

        [Fact]
        public void Fails_when_two_custom_providers_share_a_code()
        {
            ValidateOptionsResult result = Validate(
                Options(),
                new FakeProvider("apple"),
                new FakeProvider("apple"));

            Assert.True(result.Failed);
            Assert.Contains(result.Failures, f => f.Contains("more than one") && f.Contains("apple"));
        }

        [Fact]
        public void Aggregates_every_problem_into_one_result()
        {
            // Two distinct broken entries → the boot failure should list both, not stop at the first.
            ValidateOptionsResult result = Validate(Options(
                Config("google", clientId: null),   // missing ClientId
                Config("acme", clientId: "abc")));   // no authority/preset

            Assert.True(result.Failed);
            Assert.Contains(result.Failures, f => f.Contains("google"));
            Assert.Contains(result.Failures, f => f.Contains("acme"));
        }

        // ---- helpers ----

        private static ValidateOptionsResult Validate(ExternalProviderOptions options, params IExternalAuthProvider[] customProviders)
            // string.Empty is Options.DefaultName (the unnamed-options name); the validator ignores it and checks regardless.
            => new ExternalProviderOptionsValidator(customProviders).Validate(string.Empty, options);

        private static ExternalProviderOptions Options(params ExternalProviderConfig[] configs)
            => new() { ExternalProviders = configs.ToList() };

        private static ExternalProviderConfig Config(string code, string? authority = null, string? clientId = null)
            => new() { Code = code, Authority = authority, ClientId = clientId };

        private sealed class FakeProvider : IExternalAuthProvider
        {
            public FakeProvider(string code) => Code = code;
            public string Code { get; }
            public Task<ExternalIdentity> ValidateAsync(string idToken) => Task.FromResult<ExternalIdentity>(null!);
        }
    }
}
