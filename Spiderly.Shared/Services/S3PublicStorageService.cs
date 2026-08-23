using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace Spiderly.Shared.Services
{
    public class S3PublicStorageService : IFileManager
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _endpoint;
        private readonly ILogger<S3PublicStorageService> _logger;

        public S3PublicStorageService(IAmazonS3 s3Client, ILogger<S3PublicStorageService> logger, IOptions<S3Options> s3Options)
        {
            S3Options s3Settings = s3Options.Value;
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _bucketName = s3Settings.S3BucketName ?? throw new ArgumentNullException(nameof(S3Options.S3BucketName));
            _endpoint = s3Settings.S3PublicEndpoint ?? throw new ArgumentNullException(nameof(S3Options.S3PublicEndpoint));
            _logger = logger;
        }

        /// <returns>Image URL</returns>
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

            FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileName, out string? contentType))
            {
                contentType = "application/octet-stream";
            }

            PutObjectRequest putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = newFileName,
                InputStream = content,
                ContentType = contentType,
                DisablePayloadSigning = true, // Essential fix for R2
                Headers = {
                    CacheControl = "public, max-age=31536000, immutable"
                },
            };

            await _s3Client.PutObjectAsync(putRequest);

            return BuildUrl(newFileName);
        }

        public async Task DeleteNonActiveBlobs(
            string? url,
            string keyPrefix,
            string objectId)
        {
            if (BlobKeyConventions.IsStagingObjectId(objectId))
                return;

            // Nullable in step with the parameter: no active blob means every object under the prefix is
            // stale, and the != comparison below already treats null as "matches nothing".
            string? activeKey = ExtractS3KeyFromUrl(url);

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

        public async Task<string?> GetFileDataAsync(string url)
        {
            string key = ExtractS3KeyFromUrl(url);

            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Extracted S3 key is empty for URL: {Url}", url);
                return null;
            }

            try
            {
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
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("S3 key not found: {S3Key}", key);
                return null;
            }
        }

        public async Task DeleteNonActiveEditorImages(
            List<string> activeImageUrls,
            string keyPrefix,
            string objectId)
        {
            if (BlobKeyConventions.IsStagingObjectId(objectId))
                return;

            HashSet<string?> activeKeys = activeImageUrls
                .Select(ExtractS3KeyFromUrl)
                .ToHashSet();

            string prefix = $"{keyPrefix}/{objectId}/";

            ListObjectsV2Request listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            ListObjectsV2Response response = await _s3Client.ListObjectsV2Async(listRequest);

            List<S3Object> s3Objects = response.S3Objects ?? [];

            foreach (S3Object obj in s3Objects.Where(o => !activeKeys.Contains(o.Key)))
            {
                await _s3Client.DeleteObjectAsync(_bucketName, obj.Key);
            }
        }

        public async Task<string> MoveBlobToEntityPathAsync(
            string currentUrl,
            string keyPrefix,
            string objectId,
            Func<Task<string>>? resolveDescriptiveName = null)
        {
            string currentKey = ExtractS3KeyFromUrl(currentUrl);

            string? newKey = await BlobKeyConventions.TryBuildPromotedKeyAsync(currentKey, keyPrefix, objectId, resolveDescriptiveName);

            if (newKey == null)
                return currentUrl;

            await _s3Client.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = _bucketName,
                SourceKey = currentKey,
                DestinationBucket = _bucketName,
                DestinationKey = newKey,
                MetadataDirective = S3MetadataDirective.COPY,
            });

            await _s3Client.DeleteObjectAsync(_bucketName, currentKey);

            return BuildUrl(newKey);
        }

        private string BuildUrl(string key) =>
            $"{_endpoint.TrimEnd('/')}/{key}";

        [return: NotNullIfNotNull(nameof(urlOrKey))]
        private string? ExtractS3KeyFromUrl(string? urlOrKey)
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
