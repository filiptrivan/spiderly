namespace Spiderly.Shared.Interfaces
{
    public interface IFileManager
    {
        /// <returns>Newly generated file name</returns>
        Task<string> UploadFileAsync(string fileName, string objectType, string objectProperty, string objectId, Stream content, string newFileName = null);

        // Before this in save method the authorization is being done, so we don't need to do it here also
        Task DeleteNonActiveBlobs(string activeBlobName, string objectType, string objectProperty, string objectId);

        Task<string> GetFileDataAsync(string key);
    }
}
