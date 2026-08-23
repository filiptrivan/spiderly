using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// Provider-neutral conventions for constructing and inspecting blob storage keys.
    /// Keys follow <c>{KeyPrefix}/{ObjectId}/{FileName}</c>, where <c>{KeyPrefix}</c> defaults to
    /// <c>{EntityName}/{PropertyName}</c> and is overridable per property via
    /// <c>[…Storage(KeyPrefix = "…")]</c>, and <c>{FileName}</c> is either
    /// <c>{Slug}-{8charSuffix}.{ext}</c> when the entity supplies a descriptive name (keys are
    /// public, indexed URLs — the slug is the descriptive signal, the random suffix is what makes
    /// per-upload immutable caching safe) or <c>{BlobGuid}.{ext}</c> when it doesn't. Inserts
    /// (objectId "0" or empty) route through the <c>{KeyPrefix}/_tmp/{UploadGuid}/</c> staging
    /// prefix until the entity is saved and the blob is promoted — descriptive names apply at
    /// promotion, never in staging (no trusted entity exists yet).
    /// </summary>
    public static class BlobKeyConventions
    {
        public const string StagingSegment = "_tmp";

        /// <summary>
        /// Hard cap on the slug segment of a blob key. Keys are public URLs; the slug carries
        /// the descriptive signal and anything past ~60 chars is noise that only bloats the URL.
        /// </summary>
        public const int MaxSlugLength = 60;

        /// <summary>
        /// Folds an arbitrary descriptive name (an entity slug, a raw display name, anything a
        /// consumer's <c>GetBlobDescriptiveName…</c> hook returns) into a key-safe ASCII slug:
        /// lowercase, digits and dashes only, diacritics transliterated (<c>đ→dj</c>, <c>š→s</c>,
        /// <c>ü→u</c>, …), separator runs collapsed, capped at <see cref="MaxSlugLength"/>.
        /// Returns <c>null</c> when nothing usable remains — callers must treat that as "no
        /// descriptive segment", never emit an empty one.
        /// </summary>
        public static string? SlugifyDescriptiveName(string? descriptiveName)
        {
            if (string.IsNullOrWhiteSpace(descriptiveName))
                return null;

            // đ/Đ is a stroked letter, not a base-letter + combining-mark pair, so the FormD
            // decomposition below cannot fold it — and its conventional ASCII form is a digraph.
            string value = descriptiveName.Replace("đ", "dj").Replace("Đ", "dj");

            // FormD splits accented letters into base char + combining mark; skipping the marks
            // folds š→s, č→c, ž→z, ü→u, é→e … without a per-language table.
            StringBuilder builder = new(value.Length);
            foreach (char c in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsAsciiLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
                else if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-'); // any other char is a separator; leading/doubled runs never start
            }

            string slug = builder.ToString();

            if (slug.Length > MaxSlugLength)
                slug = slug[..MaxSlugLength];

            slug = slug.TrimEnd('-');

            return slug.Length == 0 ? null : slug;
        }

        /// <summary>
        /// Builds the storage key for an upload. With a usable <paramref name="descriptiveName"/>
        /// the file segment is <c>{slug}-{8charSuffix}.{ext}</c>; without one it is
        /// <c>{Guid}.{ext}</c>. Staging ids route to the <c>_tmp</c> prefix and never carry a
        /// descriptive name (see class docs).
        /// </summary>
        public static string BuildKey(string fileName, string keyPrefix, string objectId, string? descriptiveName = null)
        {
            string extension = Helper.GetFileExtensionFromFileName(fileName);

            return IsStagingObjectId(objectId)
                ? $"{keyPrefix}/{StagingSegment}/{Guid.NewGuid()}/{Guid.NewGuid()}.{extension}"
                : $"{keyPrefix}/{objectId}/{BuildFileSegment(descriptiveName)}.{extension}";
        }

        public static bool IsStagingObjectId(string objectId) =>
            string.IsNullOrEmpty(objectId) || objectId == "0";

        public static bool IsStagingKey(string key, string keyPrefix) =>
            !string.IsNullOrEmpty(key)
            && key.StartsWith($"{keyPrefix}/{StagingSegment}/", StringComparison.Ordinal);

        /// <summary>
        /// Returns <c>true</c> and emits the permanent-path key when the current key is a
        /// staged upload that needs promotion. Returns <c>false</c> (leaving <paramref name="newKey"/>
        /// null) when the move should be skipped — either because the key is already permanent
        /// or because no real object id is available yet. Promotion is where a staged upload
        /// first meets its saved entity, so this is where the descriptive name lands in the key.
        /// </summary>
        public static bool TryBuildPromotedKey(string currentKey, string keyPrefix, string objectId, [NotNullWhen(true)] out string? newKey, string? descriptiveName = null)
        {
            if (string.IsNullOrEmpty(currentKey)
                || IsStagingObjectId(objectId)
                || !IsStagingKey(currentKey, keyPrefix))
            {
                newKey = null;
                return false;
            }

            string extension = Helper.GetFileExtensionFromFileName(currentKey);
            newKey = $"{keyPrefix}/{objectId}/{BuildFileSegment(descriptiveName)}.{extension}";
            return true;
        }

        /// <summary>
        /// The extensionless file segment: slug plus an 8-char random suffix, or a bare GUID when
        /// no usable descriptive name exists. The random part is load-bearing either way — blobs
        /// are served with <c>Cache-Control: immutable</c>, so every upload must mint a new key
        /// or replaced content would be cached stale for up to a year.
        /// </summary>
        private static string BuildFileSegment(string? descriptiveName)
        {
            string? slug = SlugifyDescriptiveName(descriptiveName);

            return slug == null
                ? Guid.NewGuid().ToString()
                : $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
        }
    }
}
