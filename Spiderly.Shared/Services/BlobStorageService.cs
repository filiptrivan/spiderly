using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

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
        public async Task<string> UploadFileAsync(string fileName, string objectType, string objectProperty, string objectId, Stream content)
        {
            string fileExtension = Helper.GetFileExtensionFromFileName(fileName);

            // TODO FT: Delete class name and prop name if you don't need it
            // TODO FT: Validate if user has changed ContentType to something we don't handle
            string blobName = $"{objectId}-{Guid.NewGuid()}.{fileExtension}";

            BlobClient blobClient = _blobContainerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(content);

            Dictionary<string, string> tags = new()
            {
                { "objectType", $"{objectType}" },
                { "objectProperty", $"{objectProperty}" },
                { "objectId", $"{objectId}" },
            };

            await blobClient.SetTagsAsync(tags); // https://stackoverflow.com/questions/52769758/azure-blob-storage-authorization-permission-mismatch-error-for-get-request-wit 

            return blobName;
        }

        // FT: Before this in save method the authorization is being done, so we don't need to do it here also
        public async Task DeleteNonActiveBlobs(string activeBlobName, string objectType, string objectProperty, string objectId)
        {
            if (objectId == "0") // FT: If we delete 0, we will delete the blob for multiple users/partners/etc.
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
    }
}
