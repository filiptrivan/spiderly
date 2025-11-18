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
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
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
            // TODO: Do null validation for every argument of the method in Helper method
            // TODO FT: Validate if user has changed ContentType to something we don't handle

            string fileExtension = Helper.GetFileExtensionFromFileName(fileName);
            string key = newFileName ?? $"{objectType}/{objectProperty}/{objectId}/{objectId}-{Guid.NewGuid()}.{fileExtension}";

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = content
            };

            await _s3Client.PutObjectAsync(putRequest);

            return key;
        }

        public async Task DeleteNonActiveBlobs(
            string activeBlobName,
            string objectType,
            string objectProperty,
            string objectId)
        {
            if (objectId == "0") // If we delete 0, we will delete the blob for multiple users/partners/etc.
                return;

            string prefix = $"{objectType}/{objectProperty}/{objectId}/";

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            ListObjectsV2Response listResponse;
            do
            {
                listResponse = await _s3Client.ListObjectsV2Async(listRequest);

                foreach (var s3Object in listResponse.S3Objects)
                {
                    if (s3Object.Key != activeBlobName)
                    {
                        await _s3Client.DeleteObjectAsync(_bucketName, s3Object.Key);
                    }
                }

                listRequest.ContinuationToken = listResponse.NextContinuationToken;
            } while (listResponse.IsTruncated == true);
        }

        public async Task<string> GetFileDataAsync(string key)
        {
            var getRequest = new GetObjectRequest
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
    }
}