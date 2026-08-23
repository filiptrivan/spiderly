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
    }
}
