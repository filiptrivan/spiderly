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
    /// <c>d</c>, not the digraph <c>dj</c>), the length cap is an SEO preference, and the random
    /// suffix solves immutable-cache staleness that a consumer serving <c>no-cache</c> does not
    /// have. What is deliberately NOT configurable is the <c>{prefix}/{objectId}/</c> path
    /// structure — blob cleanup and staging promotion scope by listing it, so a consumer changing
    /// it would silently break blob deletion rather than merely renaming things.
    /// </para>
    /// </summary>
    public class BlobKeyOptions
    {
        /// <summary>The defaults, used by every <see cref="Helpers.BlobKeyConventions"/> overload called without options.</summary>
        public static readonly BlobKeyOptions Default = new();

        /// <summary>
        /// Hard cap on the slug segment, applied at the last word boundary that fits. Keys are
        /// public URLs and anything past ~60 chars is noise; raise it if your slugs carry meaning
        /// further in, lower it if your storage or CDN has a tighter key budget.
        /// </summary>
        public int MaxSlugLength { get; set; } = 60;

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
