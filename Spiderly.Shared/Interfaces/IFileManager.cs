namespace Spiderly.Shared.Interfaces
{
    public interface IFileManager
    {
        /// <returns>Newly generated file name</returns>
        Task<string> UploadFileAsync(string fileName, string objectType, string objectProperty, string objectId, Stream content, string? newFileName = null);

        // Before this in save method the authorization is being done, so we don't need to do it here also
        Task DeleteNonActiveBlobs(string activeBlobName, string objectType, string objectProperty, string objectId);

        Task DeleteNonActiveEditorImages(List<string> activeImageUrls, string objectType, string objectProperty, string objectId);

        // Null when the blob no longer exists in storage (see S3PublicStorageService); disk/private implementations throw instead.
        Task<string?> GetFileDataAsync(string key);

        /// <summary>
        /// Moves a blob that was uploaded to the temporary staging prefix to its permanent
        /// entity-scoped path. Called by generated save flow once the entity has a real id.
        /// Returns the new key (or url for providers that return urls). If the supplied
        /// <paramref name="currentKeyOrUrl"/> is not under the staging prefix the value is
        /// returned unchanged.
        /// </summary>
        Task<string> MoveBlobToEntityPathAsync(string currentKeyOrUrl, string objectType, string objectProperty, string objectId);
    }
}
