using Xunit;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins the descriptive-name slugifier that feeds blob key construction
    /// (<see cref="BlobKeyConventions"/>). Blob keys are public, indexed URLs, so whatever a
    /// consumer's <c>GetBlobDescriptiveName…</c> hook returns — an already-clean slug, a raw
    /// product name, a name with diacritics — must come out as a key-safe ASCII slug or as
    /// <c>null</c> (meaning "no descriptive segment", never an empty/broken key part).
    /// </summary>
    public class BlobKeyConventionsTests
    {
        [Theory]
        [InlineData("bosch-gsb-13-re", "bosch-gsb-13-re")] // already-clean slug passes through
        [InlineData("Bosch GSB 13 RE", "bosch-gsb-13-re")] // raw display name
        [InlineData("Akumulatorske bušilice — šrafilice", "akumulatorske-busilice-srafilice")] // sr diacritics + em dash
        [InlineData("đačka ĐŽŠĆČ žica", "djacka-djzscc-zica")] // đ/Đ transliterate to dj, not a dropped char
        [InlineData("Küche & Bar café", "kuche-bar-cafe")] // latin diacritics beyond sr fold to ASCII
        // Single-code-point letters FormD cannot decompose: without the table they'd vanish as
        // separators, so a Polish/Nordic/German consumer would silently lose letters.
        [InlineData("Łopata ogrodowa", "lopata-ogrodowa")]
        [InlineData("Køb saugust ø", "kob-saugust-o")]
        [InlineData("Straße Æther œuvre", "strasse-aether-oeuvre")]
        [InlineData("a  --  b", "a-b")] // separator runs collapse to one dash
        [InlineData("-leading and trailing-", "leading-and-trailing")]
        [InlineData("100% pamuk (bela)", "100-pamuk-bela")]
        public void SlugifyDescriptiveName_ProducesKeySafeAsciiSlug(string input, string expected)
        {
            Assert.Equal(expected, BlobKeyConventions.SlugifyDescriptiveName(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("!!! ??? ***")] // nothing usable left → null, never "" or "-"
        public void SlugifyDescriptiveName_ReturnsNullWhenNothingUsable(string? input)
        {
            Assert.Null(BlobKeyConventions.SlugifyDescriptiveName(input));
        }

        [Theory]
        [InlineData("bosch-gsb-13-re", @"^proizvodi/84512/bosch-gsb-13-re-[0-9a-f]{8}\.jpg$")] // slug + short per-upload suffix
        [InlineData("Bosch GSB 13 RE", @"^proizvodi/84512/bosch-gsb-13-re-[0-9a-f]{8}\.jpg$")] // raw name is slugified
        [InlineData(null, @"^proizvodi/84512/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.jpg$")] // no name → today's GUID key
        [InlineData("!!!", @"^proizvodi/84512/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.jpg$")] // unusable name falls back to GUID
        public void BuildKey_ComposesPrefixIdAndDescriptiveName(string? descriptiveName, string expectedPattern)
        {
            string key = BlobKeyConventions.BuildKey("photo.jpg", "proizvodi", "84512", descriptiveName);

            Assert.Matches(expectedPattern, key);
        }

        [Fact]
        public void BuildKey_StagingIdRoutesToStagingPrefixAndIgnoresDescriptiveName()
        {
            // Staged uploads exist before the entity does, so no descriptive name can be trusted
            // yet; the slug is applied at promotion instead.
            string key = BlobKeyConventions.BuildKey("photo.jpg", "proizvodi", "0", "bosch-gsb-13-re");

            Assert.Matches(@"^proizvodi/_tmp/[0-9a-f-]{36}/[0-9a-f-]{36}\.jpg$", key);
        }

        [Fact]
        public void TryBuildPromotedKey_AppliesDescriptiveNameAtPromotion()
        {
            string stagingKey = $"proizvodi/_tmp/{Guid.NewGuid()}/{Guid.NewGuid()}.jpg";

            bool promoted = BlobKeyConventions.TryBuildPromotedKey(
                stagingKey, "proizvodi", "84512", out string? newKey, "Bosch GSB 13 RE");

            Assert.True(promoted);
            Assert.Matches(@"^proizvodi/84512/bosch-gsb-13-re-[0-9a-f]{8}\.jpg$", newKey);
        }

        [Fact]
        public void TryBuildPromotedKey_LeavesPermanentKeysAlone()
        {
            bool promoted = BlobKeyConventions.TryBuildPromotedKey(
                "proizvodi/84512/bosch-gsb-13-re-3f9a21c4.jpg", "proizvodi", "84512", out string? newKey, "bosch-gsb-13-re");

            Assert.False(promoted);
            Assert.Null(newKey);
        }

        [Fact]
        public void SlugifyDescriptiveName_CapsLengthWithoutTrailingDash()
        {
            // 7 chars per "word-" block × 20 = 140 chars raw; cap must cut cleanly, not mid-dash.
            string longName = string.Join(" ", Enumerable.Repeat("abcdef", 20));

            string? slug = BlobKeyConventions.SlugifyDescriptiveName(longName);

            Assert.NotNull(slug);
            Assert.True(slug!.Length <= 60, $"slug length {slug.Length} exceeds cap: {slug}");
            Assert.False(slug.EndsWith('-'), "capped slug must not end with a dash");
        }

        [Fact]
        public void SlugifyDescriptiveName_CapsOnAWordBoundaryNotMidWord()
        {
            // A real over-cap product name: 26% of the PACMS catalog exceeds the cap, so a
            // mid-word cut ("…-baterij") would be the normal look of a public image URL.
            string? slug = BlobKeyConventions.SlugifyDescriptiveName(
                "Akumulatorska udarna busilica odvijac sa dve baterije i koferom");

            Assert.Equal("akumulatorska-udarna-busilica-odvijac-sa-dve-baterije-i", slug);
        }

        [Fact]
        public void SlugifyDescriptiveName_HardCutsWhenTheFirstWordExceedsTheCap()
        {
            string? slug = BlobKeyConventions.SlugifyDescriptiveName(new string('a', 80));

            Assert.Equal(new string('a', 60), slug);
        }

        [Fact]
        public async Task TryBuildPromotedKeyAsync_ResolvesTheDescriptiveNameOnlyWhenPromoting()
        {
            // The factory is typically a DB query and every save of a blob-carrying entity calls
            // this, so a no-promotion save must never invoke it. This is the invariant the three
            // adapters used to hand-copy as a pre-guard.
            int resolveCalls = 0;
            Task<string> Resolve() { resolveCalls++; return Task.FromResult("Bosch GSB 13 RE"); }

            string? skipped = await BlobKeyConventions.TryBuildPromotedKeyAsync(
                "proizvodi/84512/already-permanent-3f9a21c4.jpg", "proizvodi", "84512", Resolve);

            Assert.Null(skipped);
            Assert.Equal(0, resolveCalls);

            string? promoted = await BlobKeyConventions.TryBuildPromotedKeyAsync(
                $"proizvodi/_tmp/{Guid.NewGuid()}/{Guid.NewGuid()}.jpg", "proizvodi", "84512", Resolve);

            Assert.Matches(@"^proizvodi/84512/bosch-gsb-13-re-[0-9a-f]{8}\.jpg$", promoted);
            Assert.Equal(1, resolveCalls);
        }

        [Fact]
        public async Task TryBuildPromotedKeyAsync_WithoutAFactoryFallsBackToTheGuidKey()
        {
            string? promoted = await BlobKeyConventions.TryBuildPromotedKeyAsync(
                $"proizvodi/_tmp/{Guid.NewGuid()}/{Guid.NewGuid()}.jpg", "proizvodi", "84512", null);

            Assert.Matches(@"^proizvodi/84512/[0-9a-f-]{36}\.jpg$", promoted);
        }

        [Theory]
        [InlineData("Brand", "Image", false, "Brand/Image")]
        [InlineData("Product", "HtmlDescription", true, "Product/HtmlDescriptionImage")]
        public void DefaultKeyPrefix_IsTheSingleHomeOfTheConvention(string entity, string property, bool editorImage, string expected)
        {
            Assert.Equal(expected, BlobKeyConventions.DefaultKeyPrefix(entity, property, editorImage));
        }
    }
}
