namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// Provider-neutral conventions for constructing and inspecting blob storage keys.
    /// Keys follow <c>{EntityName}/{PropertyName}/{ObjectId}/{BlobGuid}.{ext}</c>, with
    /// inserts (objectId "0" or empty) routed through the <c>_tmp/{UploadGuid}/</c> staging
    /// prefix until the entity is saved and the blob is promoted.
    /// </summary>
    public static class BlobKeyConventions
    {
        public const string StagingSegment = "_tmp";

        public static string BuildKey(string fileName, string objectType, string objectProperty, string objectId)
        {
            string extension = Helper.GetFileExtensionFromFileName(fileName);

            return IsStagingObjectId(objectId)
                ? $"{objectType}/{objectProperty}/{StagingSegment}/{Guid.NewGuid()}/{Guid.NewGuid()}.{extension}"
                : $"{objectType}/{objectProperty}/{objectId}/{Guid.NewGuid()}.{extension}";
        }

        public static bool IsStagingObjectId(string objectId) =>
            string.IsNullOrEmpty(objectId) || objectId == "0";

        public static bool IsStagingKey(string key, string objectType, string objectProperty) =>
            !string.IsNullOrEmpty(key)
            && key.StartsWith($"{objectType}/{objectProperty}/{StagingSegment}/", StringComparison.Ordinal);

        /// <summary>
        /// Returns <c>true</c> and emits the permanent-path key when the current key is a
        /// staged upload that needs promotion. Returns <c>false</c> (leaving <paramref name="newKey"/>
        /// null) when the move should be skipped — either because the key is already permanent
        /// or because no real object id is available yet.
        /// </summary>
        public static bool TryBuildPromotedKey(string currentKey, string objectType, string objectProperty, string objectId, out string newKey)
        {
            if (string.IsNullOrEmpty(currentKey)
                || IsStagingObjectId(objectId)
                || !IsStagingKey(currentKey, objectType, objectProperty))
            {
                newKey = null;
                return false;
            }

            string extension = Helper.GetFileExtensionFromFileName(currentKey);
            newKey = $"{objectType}/{objectProperty}/{objectId}/{Guid.NewGuid()}.{extension}";
            return true;
        }
    }
}
