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
            if (newFileName == null)
            {
                newFileName = BlobKeyConventions.BuildKey(fileName, objectType, objectProperty, objectId);
            }

            BlobClient blobClient = _blobContainerClient.GetBlobClient(newFileName);

            await blobClient.UploadAsync(content);

            // https://stackoverflow.com/questions/52769758/azure-blob-storage-authorization-permission-mismatch-error-for-get-request-wit
            await blobClient.SetTagsAsync(BuildTags(objectType, objectProperty, objectId));

            return newFileName;
        }

        /// <summary>
        /// Deletes all blobs for the given object except the active one.
        /// Authorization is handled by the calling save method.
        /// </summary>
        public async Task DeleteNonActiveBlobs(string activeBlobName, string objectType, string objectProperty, string objectId)
        {
            if (BlobKeyConventions.IsStagingObjectId(objectId))
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

        public async Task<string> MoveBlobToEntityPathAsync(
            string currentKey,
            string objectType,
            string objectProperty,
            string objectId)
        {
            if (!BlobKeyConventions.TryBuildPromotedKey(currentKey, objectType, objectProperty, objectId, out string newKey))
                return currentKey;

            BlobClient sourceBlob = _blobContainerClient.GetBlobClient(currentKey);
            BlobClient destBlob = _blobContainerClient.GetBlobClient(newKey);

            // Tags applied atomically with the copy — saves a separate SetTagsAsync round-trip.
            CopyFromUriOperation copyOp = await destBlob.StartCopyFromUriAsync(
                sourceBlob.Uri,
                new BlobCopyFromUriOptions
                {
                    Tags = BuildTags(objectType, objectProperty, objectId),
                });
            await copyOp.WaitForCompletionAsync();

            await sourceBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

            return newKey;
        }

        private static Dictionary<string, string> BuildTags(string objectType, string objectProperty, string objectId) =>
            new()
            {
                { "objectType", objectType },
                { "objectProperty", objectProperty },
                { "objectId", objectId },
            };
    }
}
