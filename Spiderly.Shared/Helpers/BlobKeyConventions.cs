using System.Collections.Generic;
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
        /// Letters the FormD decomposition in <see cref="SlugifyDescriptiveName"/> cannot fold,
        /// because they are single code points rather than a base letter plus a combining mark
        /// (stroked letters, ligatures, eth/thorn). Without this table they would be dropped as
        /// separators — <c>łopata</c> would slug to <c>opata</c>, losing a letter silently.
        /// </summary>
        private static readonly Dictionary<char, string> UndecomposableLetters = new()
        {
            ['đ'] = "dj", ['Đ'] = "dj", // Serbo-Croatian; conventionally a digraph
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
        /// Folds an arbitrary descriptive name (an entity slug, a raw display name, anything a
        /// consumer's <c>GetBlobDescriptiveName…</c> hook returns) into a key-safe ASCII slug:
        /// lowercase, digits and dashes only, diacritics transliterated (<c>š→s</c>, <c>ü→u</c>,
        /// plus the <see cref="UndecomposableLetters"/> table), separator runs collapsed, and
        /// capped at <see cref="MaxSlugLength"/> on a word boundary. Returns <c>null</c> when
        /// nothing usable remains — callers must treat that as "no descriptive segment", never
        /// emit an empty one.
        /// </summary>
        public static string? SlugifyDescriptiveName(string? descriptiveName)
        {
            if (string.IsNullOrWhiteSpace(descriptiveName))
                return null;

            // FormD splits accented letters into base char + combining mark; skipping the marks
            // folds š→s, č→c, ž→z, ü→u, é→e. Single-code-point letters need the table above.
            string normalized = descriptiveName!.Normalize(NormalizationForm.FormD);

            StringBuilder builder = new(normalized.Length);
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsAsciiLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
                else if (UndecomposableLetters.TryGetValue(c, out string? transliterated))
                    builder.Append(transliterated);
                else if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-'); // any other char is a separator; leading/doubled runs never start
            }

            // TrimEnd before the cap too: a trailing non-alphanumeric already appended a separator.
            string slug = builder.ToString().TrimEnd('-');

            if (slug.Length > MaxSlugLength)
                slug = CapOnWordBoundary(slug);

            return slug.Length == 0 ? null : slug;
        }

        /// <summary>
        /// The key prefix a property gets when it declares no custom <c>KeyPrefix</c>. Single home
        /// of the default convention, so consumers that need the prefix as a runtime value never
        /// re-spell it (a second spelling drifts silently — it produces a valid but wrong key).
        /// </summary>
        public static string DefaultKeyPrefix(string entityName, string propertyName, bool isEditorImagePath = false) =>
            isEditorImagePath
                ? $"{entityName}/{propertyName}Image"
                : $"{entityName}/{propertyName}";

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
        /// The whole promotion decision for an <see cref="Interfaces.IFileManager"/> adapter:
        /// returns the permanent-path key for a staged upload, or <c>null</c> when the move
        /// should be skipped (key already permanent, or no real object id yet).
        /// <para>
        /// <paramref name="resolveDescriptiveName"/> is awaited ONLY when a promotion is actually
        /// due — that ordering is the whole reason it is a factory rather than a value, since
        /// resolving it is typically a database query, and every save of an entity with a blob
        /// property calls this. Adapters must call this rather than hand-rolling the guard, or
        /// they silently pay that query on every save.
        /// </para>
        /// </summary>
        public static async Task<string?> TryBuildPromotedKeyAsync(
            string currentKey,
            string keyPrefix,
            string objectId,
            Func<Task<string>>? resolveDescriptiveName)
        {
            if (IsStagingObjectId(objectId) || !IsStagingKey(currentKey, keyPrefix))
                return null;

            string? descriptiveName = resolveDescriptiveName == null ? null : await resolveDescriptiveName();

            return TryBuildPromotedKey(currentKey, keyPrefix, objectId, out string? newKey, descriptiveName)
                ? newKey
                : null;
        }

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
        /// Truncates to <see cref="MaxSlugLength"/> at the last word boundary rather than
        /// mid-word: a cut like <c>…-odvijac-sa-dve-baterij</c> reads as a typo in a public URL.
        /// Falls back to a hard cut when the first word alone exceeds the cap.
        /// </summary>
        private static string CapOnWordBoundary(string slug)
        {
            string capped = slug[..MaxSlugLength];
            int lastSeparator = capped.LastIndexOf('-');

            return lastSeparator > 0 ? capped[..lastSeparator] : capped;
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
