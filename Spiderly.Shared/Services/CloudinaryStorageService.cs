using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Services
{
    public class CloudinaryStorageService : IFileManager
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryStorageService()
        {
            string cloudinaryCloudName = SettingsProvider.Current.CloudinaryCloudName ?? throw new ArgumentNullException($"{nameof(SettingsProvider.Current.CloudinaryCloudName)} configuration is missing");
            string cloudinaryApiKey = SettingsProvider.Current.CloudinaryApiKey ?? throw new ArgumentNullException($"{nameof(SettingsProvider.Current.CloudinaryApiKey)} configuration is missing");
            string cloudinaryApiSecret = SettingsProvider.Current.CloudinaryApiSecret ?? throw new ArgumentNullException($"{nameof(SettingsProvider.Current.CloudinaryApiSecret)} configuration is missing");

            var account = new Account(cloudinaryCloudName, cloudinaryApiKey, cloudinaryApiSecret);
            _cloudinary = new Cloudinary(account);
        }

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

            ImageUploadParams imageParams = new()
            {
                File = new FileDescription(newFileName, content),
                PublicId = newFileName,
                Overwrite = true,
                UseFilename = false,
                UniqueFilename = false
            };

            UploadResult uploadResult = await _cloudinary.UploadAsync(imageParams);

            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return newFileName;
            }

            throw new Exception(uploadResult.Error?.Message);
        }

        public async Task<string> GetFileDataAsync(string key)
        {
            GetResourceParams getResourceParams = new GetResourceParams(key)
            {
                ResourceType = ResourceType.Image
            };

            GetResourceResult result = await _cloudinary.GetResourceAsync(getResourceParams);

            if (result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return result.SecureUrl;
            }

            throw new Exception(result.Error?.Message);
        }

        public async Task DeleteNonActiveBlobs(string activeBlobName, string objectType, string objectProperty, string objectId)
        {

            if (BlobKeyConventions.IsStagingObjectId(objectId))
                return;

            string prefix = $"{objectType}/{objectProperty}/{objectId}/";

            ListResourcesByPrefixParams listParams = new ListResourcesByPrefixParams()
            {
                Type = "upload",
                Prefix = prefix,
                MaxResults = 500
            };

            var listResult = await _cloudinary.ListResourcesAsync(listParams);

            if (listResult.Resources == null || listResult.Resources.Length == 0)
                return;

            var resourcesToDelete = listResult.Resources
                .Where(r => r.PublicId != activeBlobName)
                .Select(r => r.PublicId)
                .ToList();

            if (resourcesToDelete.Count == 0)
                return;

            var batches = resourcesToDelete
                .Select((id, index) => new { id, index })
                .GroupBy(x => x.index / 100)
                .Select(g => g.Select(x => x.id).ToList());

            foreach (var batch in batches)
            {
                var deleteParams = new DelResParams()
                {
                    PublicIds = batch,
                    Invalidate = true
                };

                var deleteResult = await _cloudinary.DeleteResourcesAsync(deleteParams);
            }
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

            RenameResult renameResult = await _cloudinary.RenameAsync(new RenameParams(currentKey, newKey)
            {
                Overwrite = false,
                Invalidate = true,
            });

            if (renameResult.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception(renameResult.Error?.Message ?? "Cloudinary rename failed.");

            return newKey;
        }
    }
}
