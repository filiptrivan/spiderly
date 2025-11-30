using Amazon.S3;
using Amazon.S3.Model;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Services
{
    public class S3PublicStorageService : IFileManager
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _endpoint;

        public S3PublicStorageService(IAmazonS3 s3Client)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _bucketName = SettingsProvider.Current.S3BucketName ?? throw new ArgumentNullException(nameof(SettingsProvider.Current.S3BucketName));
            _endpoint = SettingsProvider.Current.S3PublicEndpoint ?? throw new ArgumentNullException(nameof(SettingsProvider.Current.S3PublicEndpoint));
        }

        /// <returns>Image URL</returns>
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
            // TODO: Validate if user has changed ContentType to something we don't handle

            if (newFileName == null)
            {
                string fileExtension = Helper.GetFileExtensionFromFileName(fileName);
                newFileName = $"{objectType}/{objectProperty}/{objectId}/{objectId}-{Guid.NewGuid()}.{fileExtension}";
            }

            PutObjectRequest putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = newFileName,
                InputStream = content,
                DisablePayloadSigning = true, // Essential fix for R2
            };

            await _s3Client.PutObjectAsync(putRequest);

            return $"{_endpoint.TrimEnd('/')}/{newFileName}";
        }

        public async Task DeleteNonActiveBlobs(
            string url,
            string objectType,
            string objectProperty,
            string objectId)
        {
            if (objectId == "0") // If we delete 0, we will delete the blob for multiple users/partners/etc.
                return;

            string activeKey = ExtractS3KeyFromUrl(url);

            string prefix = $"{objectType}/{objectProperty}/{objectId}/";

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

        public async Task<string> GetFileDataAsync(string url)
        {
            string key = ExtractS3KeyFromUrl(url);

            GetObjectRequest getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            using GetObjectResponse response = await _s3Client.GetObjectAsync(getRequest);
            using MemoryStream memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);

            byte[] byteArray = memoryStream.ToArray();
            string base64 = Convert.ToBase64String(byteArray);

            return $"filename={key};base64,{base64}";
        }

        private string ExtractS3KeyFromUrl(string urlOrKey)
        {
            if (urlOrKey?.StartsWith("http") == true)
            {
                Uri uri = new Uri(urlOrKey);
                return uri.AbsolutePath.TrimStart('/');
            }

            return urlOrKey;
        }
    }
}
