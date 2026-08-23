using System.Collections.Generic;

namespace Spiderly.Shared
{
    /// <summary>
    /// Policy knobs for how <see cref="Helpers.BlobKeyConventions"/> names uploaded blobs. Every
    /// value has a working default, so a consumer that never registers this gets today's behavior.
    /// Register with <c>services.Configure&lt;BlobKeyOptions&gt;(…)</c> to change it.
    /// <para>
    /// The knobs exist because the defaults encode judgements that are NOT universal: the
    /// transliteration table is Latin/Serbo-Croatian-shaped (Vietnamese <c>đ</c> is the letter
    /// <c>d</c>, not the digraph <c>dj</c>), the length cap is a storage-derived backstop rather
    /// than a preference, and the random suffix solves immutable-cache staleness that a consumer
    /// serving <c>no-cache</c> does not have. What is deliberately NOT configurable is the <c>{prefix}/{objectId}/</c> path
    /// structure — blob cleanup and staging promotion scope by listing it, so a consumer changing
    /// it would silently break blob deletion rather than merely renaming things.
    /// </para>
    /// </summary>
    public class BlobKeyOptions
    {
        /// <summary>The defaults, used by every <see cref="Helpers.BlobKeyConventions"/> overload called without options.</summary>
        public static readonly BlobKeyOptions Default = new();

        /// <summary>
        /// Hard cap on the slug segment, applied at the last word boundary that fits.
        /// <para>
        /// This is a SAFETY BACKSTOP, not a style preference — set it from what breaks, not from
        /// what looks tidy. The descriptive name comes from a consumer hook that can return
        /// anything: point it at an HTML description and, with no cap, the upload FAILS rather
        /// than looking ugly, because <c>{slug}-{suffix}.{ext}</c> is a single filesystem
        /// component (255 bytes on ext4/APFS/NTFS — verified) and an S3 key is capped at 1024
        /// bytes. The default leaves headroom under both, and keeps a typical Windows dev path
        /// under <c>MAX_PATH</c> for <c>DiskStorageService</c>.
        /// </para>
        /// <para>
        /// Truncation is silent and permanent — the key is immutable once the object exists — so
        /// the default errs high deliberately. Measured on a 71.472-product tool catalogue
        /// (2026-08-23) where the manufacturer article number is usually the LAST token of a
        /// name: a 60-char cap stripped it from 14,8% of the catalogue, 100 from 0,68%, and 200
        /// truncates 3 products in total. If you want shorter URLs, that is a consumer decision —
        /// lower this — but know it is paid for in information the URL can never get back.
        /// </para>
        /// </summary>
        public int MaxSlugLength { get; set; } = DefaultMaxSlugLength;

        /// <summary>The value <see cref="MaxSlugLength"/> starts at. See it for what the number is derived from.</summary>
        public const int DefaultMaxSlugLength = 200;

        /// <summary>
        /// Length of the random hex suffix appended after the slug, 0-32.
        /// <para>
        /// <b>Setting this to 0 makes keys deterministic, and that is only safe if you know why.</b>
        /// Blobs uploaded by <c>S3PublicStorageService</c> are served
        /// <c>Cache-Control: public, max-age=31536000, immutable</c>, so a replaced file under a
        /// key that did not change stays cached — in browsers a purge cannot reach — for up to a
        /// year. Zero is for consumers who bust caches another way (versioned query strings,
        /// short max-age) or whose blobs are never replaced. It does not affect the
        /// no-descriptive-name path, which always uses a full GUID for uniqueness.
        /// </para>
        /// </summary>
        public int UniquenessSuffixLength { get; set; } = 8;

        /// <summary>
        /// Letters that Unicode NFD decomposition cannot fold to ASCII, because they are single
        /// code points rather than a base letter plus a combining mark (stroked letters,
        /// ligatures, eth/thorn). Anything not in this table and not decomposable is treated as a
        /// separator, so an unlisted letter is DROPPED — which is why this is editable rather
        /// than fixed: the defaults are one region's conventions, and they conflict with others
        /// (Vietnamese <c>đ</c> → <c>d</c>, German <c>ä</c> → <c>ae</c> where NFD gives <c>a</c>).
        /// Mutate it to correct a mapping, or replace it wholesale.
        /// </summary>
        public IDictionary<char, string> Transliterations { get; set; } = new Dictionary<char, string>
        {
            ['đ'] = "dj", ['Đ'] = "dj",
            ['ł'] = "l", ['Ł'] = "l",
            ['ø'] = "o", ['Ø'] = "o",
            ['æ'] = "ae", ['Æ'] = "ae",
            ['œ'] = "oe", ['Œ'] = "oe",
            ['ß'] = "ss",
            ['ħ'] = "h", ['Ħ'] = "h",
            ['ð'] = "d", ['Ð'] = "d",
            ['þ'] = "th", ['Þ'] = "th",
        };

        /// <summary>
        /// Replaces the built-in slugifier entirely — transliteration, ASCII folding, separator
        /// collapsing and the length cap all become yours. Return <c>null</c> (or blank) for "no
        /// descriptive segment", which falls back to the GUID key.
        /// <para>
        /// This is the escape hatch for scripts the built-in cannot serve at all: CJK text
        /// contains no ASCII alphanumerics, so the default folds it to nothing and every key
        /// silently degrades to a bare GUID. Plug a real romanization library in here instead.
        /// </para>
        /// <para>
        /// Your result is used as-is except that <c>/</c> is replaced with <c>-</c>: a slash would
        /// add a path segment and put the blob outside the prefix that cleanup and staging
        /// promotion list, which deletes files rather than renaming them.
        /// </para>
        /// </summary>
        public Func<string, string?>? Slugifier { get; set; }
    }
}
