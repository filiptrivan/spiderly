using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Services
{
    public class BlobStorageService : IFileManager
    {
        private readonly BlobContainerClient _blobContainerClient;

        public BlobStorageService(BlobContainerClient blobContainerClient)
        {
            _blobContainerClient = blobContainerClient;
        }

        /// <returns>Newly generated file name</returns>
        public async Task<string> UploadFileAsync(
            string fileName,
            string objectType,
            string objectProperty,
            string objectId,
            Stream content,
            string newFileName = null
        )
        {
            // TODO: Do null validation for every argument of the method in Helper method

            // TODO FT: Validate if user has changed ContentType to something we don't handle
            if (newFileName == null)
            {
                string fileExtension = Helper.GetFileExtensionFromFileName(fileName);
                newFileName = $"{objectId}-{Guid.NewGuid()}.{fileExtension}";
            }

            BlobClient blobClient = _blobContainerClient.GetBlobClient(newFileName);

            await blobClient.UploadAsync(content);

            Dictionary<string, string> tags = new()
            {
                { "objectType", $"{objectType}" },
                { "objectProperty", $"{objectProperty}" },
                { "objectId", $"{objectId}" },
            };

            await blobClient.SetTagsAsync(tags); // https://stackoverflow.com/questions/52769758/azure-blob-storage-authorization-permission-mismatch-error-for-get-request-wit 

            return newFileName;
        }

        // Before this in save method the authorization is being done, so we don't need to do it here also
        public async Task DeleteNonActiveBlobs(string activeBlobName, string objectType, string objectProperty, string objectId)
        {
            if (objectId == "0") // If we delete 0, we will delete the blob for multiple users/partners/etc.
                return;

            AsyncPageable<TaggedBlobItem> blobs = _blobContainerClient.FindBlobsByTagsAsync($"\"objectType\"='{objectType}' AND \"objectProperty\"='{objectProperty}' AND \"objectId\"='{objectId}'");

            await foreach (TaggedBlobItem blob in blobs)
            {
                if (blob.BlobName != activeBlobName)
                    await _blobContainerClient.DeleteBlobAsync(blob.BlobName, Azure.Storage.Blobs.Models.DeleteSnapshotsOption.IncludeSnapshots);
            }
        }

        public async Task<string> GetFileDataAsync(string key)
        {
            BlobClient blobClient = _blobContainerClient.GetBlobClient(key);

            Azure.Response<BlobDownloadResult> blobDownloadInfo = await blobClient.DownloadContentAsync();

            byte[] byteArray = blobDownloadInfo.Value.Content.ToArray();

            string base64 = Convert.ToBase64String(byteArray);

            return $"filename={key};base64,{base64}";
        }

        public Task DeleteNonActiveEditorImages(
            List<string> activeImageUrls,
            string objectType,
            string objectProperty,
            string objectId)
        {
            throw new NotImplementedException();
        }
    }
}
