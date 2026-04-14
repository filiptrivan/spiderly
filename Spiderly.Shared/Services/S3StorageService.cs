using Amazon.S3;
using Amazon.S3.Model;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Services
{
    public class S3StorageService : IFileManager
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public S3StorageService(IAmazonS3 s3Client)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _bucketName = SettingsProvider.Current.S3BucketName ?? throw new ArgumentNullException(nameof(SettingsProvider.Current.S3BucketName));
        }

        /// <returns>Newly generated file name (S3 key)</returns>
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

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = newFileName,
                InputStream = content,
                DisablePayloadSigning = true,
            };

            await _s3Client.PutObjectAsync(putRequest);

            return newFileName;
        }

        public async Task DeleteNonActiveBlobs(
            string activeKey,
            string objectType,
            string objectProperty,
            string objectId)
        {
            // Staged uploads live under _tmp/, not the per-entity folder scanned here.
            if (BlobKeyConventions.IsStagingObjectId(objectId))
                return;

            string prefix = $"{objectType}/{objectProperty}/{objectId}/";

            ListObjectsV2Request listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            ListObjectsV2Response response = await _s3Client.ListObjectsV2Async(listRequest);

            foreach (S3Object obj in response.S3Objects.Where(o => o.Key != activeKey))
            {
                await _s3Client.DeleteObjectAsync(_bucketName, obj.Key);
            }
        }

        public async Task<string> GetFileDataAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("S3 key cannot be null or empty.", nameof(key));

            GetObjectRequest getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(getRequest);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);

            byte[] byteArray = memoryStream.ToArray();
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

            await _s3Client.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = _bucketName,
                SourceKey = currentKey,
                DestinationBucket = _bucketName,
                DestinationKey = newKey,
            });

            await _s3Client.DeleteObjectAsync(_bucketName, currentKey);

            return newKey;
        }
    }
}
