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
            // 7 chars per "word-" block × 40 = 280 chars raw; cap must cut cleanly, not mid-dash.
            string longName = string.Join(" ", Enumerable.Repeat("abcdef", 40));

            string? slug = BlobKeyConventions.SlugifyDescriptiveName(longName);

            Assert.NotNull(slug);
            Assert.True(slug!.Length <= BlobKeyOptions.DefaultMaxSlugLength,
                $"slug length {slug.Length} exceeds cap: {slug}");
            Assert.False(slug.EndsWith('-'), "capped slug must not end with a dash");
        }

        [Fact]
        public void TheCapKeepsARunawayHookReturnStorable()
        {
            // What the cap is actually FOR. A consumer hook can return anything — pointing it at
            // an HTML description is an easy mistake — and without a bound the upload FAILS
            // rather than looking ugly: the file segment is one filesystem component (255 bytes)
            // and an S3 key is capped at 1024. Assert the produced segment stays storable.
            string runaway = string.Join(" ", Enumerable.Repeat("opis proizvoda", 5000)); // ~70k chars

            string key = BlobKeyConventions.BuildKey("photo.jpg", "proizvodi", "84512", runaway);
            string fileSegment = key.Split('/').Last();

            Assert.True(fileSegment.Length <= 255, $"file segment is {fileSegment.Length} bytes — a filesystem will reject it");
            Assert.True(key.Length <= 1024, $"key is {key.Length} bytes — S3 will reject it");
        }

        [Fact]
        public void DefaultMaxSlugLength_LeavesRoomForATrailingArticleNumber()
        {
            // The default errs HIGH on purpose: truncation is silent and the key is immutable, so
            // a cap set low destroys information permanently while one set high only lengthens a
            // URL. Measured on a 71.472-product tool catalogue (2026-08-23), where the
            // manufacturer article number is usually the LAST token: 60 stripped it from 14,8% of
            // the catalogue, 100 from 0,68%, 200 truncates 3 products in total. Lowering this is a
            // consumer decision (BlobKeyOptions), not a tidy-up — hence pinned.
            Assert.Equal(200, BlobKeyOptions.DefaultMaxSlugLength);

            string? slug = BlobKeyConventions.SlugifyDescriptiveName(
                "Bosch Expert rolna brusnog papira za rucno brusenje 93mm duzina 50m granulacija 240 2608900974");

            Assert.EndsWith("2608900974", slug);
        }

        [Fact]
        public void SlugifyDescriptiveName_CapsOnAWordBoundaryNotMidWord()
        {
            // Explicit small cap so this pins the boundary LOGIC rather than the default value:
            // a mid-word cut ("…-baterij") would be the normal look of a public image URL.
            BlobKeyOptions tight = new() { MaxSlugLength = 60 };

            string? slug = BlobKeyConventions.SlugifyDescriptiveName(
                "Akumulatorska udarna busilica odvijac sa dve baterije i koferom", tight);

            Assert.Equal("akumulatorska-udarna-busilica-odvijac-sa-dve-baterije-i", slug);
        }

        [Fact]
        public void SlugifyDescriptiveName_HardCutsWhenTheFirstWordExceedsTheCap()
        {
            BlobKeyOptions tight = new() { MaxSlugLength = 60 };

            string? slug = BlobKeyConventions.SlugifyDescriptiveName(new string('a', 80), tight);

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

        [Fact]
        public void Transliterations_AreCorrectableByAConsumer()
        {
            // The defaults are one region's conventions and they conflict with others: đ is the
            // digraph "dj" in Serbo-Croatian but the plain letter "d" in Vietnamese. A consumer
            // must be able to correct a mapping rather than live with wrong keys forever.
            BlobKeyOptions vietnamese = new();
            vietnamese.Transliterations['đ'] = "d";
            vietnamese.Transliterations['Đ'] = "D";

            Assert.Equal("dong-nai", BlobKeyConventions.SlugifyDescriptiveName("Đồng Nai", vietnamese));
            Assert.Equal("djong-nai", BlobKeyConventions.SlugifyDescriptiveName("Đồng Nai"));
        }

        [Fact]
        public void Slugifier_ReplacesTheBuiltInEntirely()
        {
            // The built-in folds CJK to nothing (no ASCII alphanumerics survive), so every key
            // would silently degrade to a bare GUID. This hatch is what lets that consumer plug
            // in a real romanization library.
            Assert.Null(BlobKeyConventions.SlugifyDescriptiveName("電動ドリル"));

            BlobKeyOptions romanized = new() { Slugifier = _ => "dendo-doriru" };

            Assert.Equal("dendo-doriru", BlobKeyConventions.SlugifyDescriptiveName("電動ドリル", romanized));
        }

        [Fact]
        public void Slugifier_CannotBreakTheKeyStructureWithASlash()
        {
            // A slash would add a path segment, putting the blob outside the prefix that cleanup
            // and staging promotion list — that deletes files rather than renaming them.
            BlobKeyOptions nested = new() { Slugifier = _ => "brand/model" };

            string key = BlobKeyConventions.BuildKey("photo.jpg", "products", "84512", "anything", nested);

            Assert.Matches(@"^products/84512/brand-model-[0-9a-f]{8}\.jpg$", key);
        }

        [Theory]
        [InlineData("brand/model", "brand-model")]
        [InlineData(@"brand\model", "brand-model")] // Windows separator: DiskStorageService maps / onto it
        [InlineData("../escape", "..-escape")] // folds to one literal segment — no longer traversal
        public void Slugifier_OutputCannotIntroduceAPathSegment(string custom, string expectedSlug)
        {
            BlobKeyOptions options = new() { Slugifier = _ => custom, UniquenessSuffixLength = 0 };

            Assert.Equal($"products/84512/{expectedSlug}.jpg",
                BlobKeyConventions.BuildKey("photo.jpg", "products", "84512", "anything", options));
        }

        [Fact]
        public void Slugifier_ReturningOnlyDots_FallsBackToTheGuidKey()
        {
            // ".." names a relative directory rather than a file, so it cannot be the segment.
            BlobKeyOptions options = new() { Slugifier = _ => ".." };

            Assert.Matches(@"^products/84512/[0-9a-f-]{36}\.jpg$",
                BlobKeyConventions.BuildKey("photo.jpg", "products", "84512", "anything", options));
        }

        [Fact]
        public void MaxSlugLength_AndSuffixLength_AreConfigurable()
        {
            BlobKeyOptions tight = new() { MaxSlugLength = 20, UniquenessSuffixLength = 4 };

            string key = BlobKeyConventions.BuildKey(
                "photo.jpg", "products", "84512", "Akumulatorska udarna busilica odvijac", tight);

            Assert.Matches(@"^products/84512/akumulatorska-udarna-[0-9a-f]{4}\.jpg$", key);
        }

        [Fact]
        public void UniquenessSuffixLength_Zero_ProducesDeterministicNamedKeys()
        {
            // For consumers who bust caches another way. Opting out must not also make the
            // no-descriptive-name path collide, so that path keeps its full GUID.
            BlobKeyOptions deterministic = new() { UniquenessSuffixLength = 0 };

            Assert.Equal("products/84512/bosch-gsb-13-re.jpg",
                BlobKeyConventions.BuildKey("photo.jpg", "products", "84512", "Bosch GSB 13 RE", deterministic));

            Assert.Matches(@"^products/84512/[0-9a-f-]{36}\.jpg$",
                BlobKeyConventions.BuildKey("photo.jpg", "products", "84512", null, deterministic));
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
