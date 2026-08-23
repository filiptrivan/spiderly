using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Services
{
    public class S3PrivateStorageService : IFileManager
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public S3PrivateStorageService(IAmazonS3 s3Client, IOptions<S3Options> s3Options)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _bucketName = s3Options.Value.S3BucketName ?? throw new ArgumentNullException(nameof(S3Options.S3BucketName));
        }

        /// <returns>Newly generated file name (S3 key)</returns>
        public async Task<string> UploadFileAsync(
            string fileName,
            string keyPrefix,
            string objectId,
            Stream content,
            string? descriptiveName = null,
            string? newFileName = null
        )
        {
            if (newFileName == null)
            {
                newFileName = BlobKeyConventions.BuildKey(fileName, keyPrefix, objectId, descriptiveName);
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
            string? activeKey,
            string keyPrefix,
            string objectId)
        {
            // Staged uploads live under _tmp/, not the per-entity folder scanned here.
            if (BlobKeyConventions.IsStagingObjectId(objectId))
                return;

            string prefix = $"{keyPrefix}/{objectId}/";

            ListObjectsV2Request listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            ListObjectsV2Response response = await _s3Client.ListObjectsV2Async(listRequest);
            List<S3Object> s3Objects = response.S3Objects ?? [];

            foreach (S3Object obj in s3Objects.Where(o => o.Key != activeKey))
            {
                await _s3Client.DeleteObjectAsync(_bucketName, obj.Key);
            }
        }

        public async Task<string?> GetFileDataAsync(string key)
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
            string keyPrefix,
            string objectId)
        {
            throw new NotImplementedException();
        }

        public async Task<string> MoveBlobToEntityPathAsync(
            string currentKey,
            string keyPrefix,
            string objectId,
            Func<Task<string?>>? resolveDescriptiveName = null)
        {
            if (!BlobKeyConventions.IsStagingKey(currentKey, keyPrefix) || BlobKeyConventions.IsStagingObjectId(objectId))
                return currentKey;

            string? descriptiveName = resolveDescriptiveName == null ? null : await resolveDescriptiveName();

            if (!BlobKeyConventions.TryBuildPromotedKey(currentKey, keyPrefix, objectId, out string? newKey, descriptiveName))
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
