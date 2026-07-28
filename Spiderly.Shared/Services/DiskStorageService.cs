using Microsoft.Extensions.Logging;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Services
{
    public class DiskStorageService : IFileManager
    {
        private readonly string _rootPath;
        private readonly ILogger<DiskStorageService> _logger;

        /// <summary>
        /// By default, files will be stored in:
        ///   {CurrentDirectory}/FileStorage
        /// </summary>
        public DiskStorageService(ILogger<DiskStorageService> logger)
            : this(Path.Combine(Directory.GetCurrentDirectory(), "FileStorage"), logger)
        {
        }

        public DiskStorageService(string rootPath, ILogger<DiskStorageService> logger)
        {
            _rootPath = rootPath;
            _logger = logger;
            Directory.CreateDirectory(_rootPath);
        }

        /// <summary>
        /// Builds a hierarchical key "{objectType}/{objectProperty}/{objectId}/{GUID}.{ext}"
        /// (or "{objectType}/{objectProperty}/_tmp/{uploadGuid}/{GUID}.{ext}" for inserts),
        /// creates the intermediate directories, and writes the stream.
        /// Returns the relative key (forward-slash separated) — same semantics as S3.
        /// </summary>
        public async Task<string> UploadFileAsync(
            string fileName,
            string objectType,
            string objectProperty,
            string objectId,
            Stream content,
            string? newFileName = null
        )
        {

            if (newFileName == null)
            {
                newFileName = BlobKeyConventions.BuildKey(fileName, objectType, objectProperty, objectId);
            }

            string fullPath = Path.Combine(_rootPath, newFileName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using FileStream fileStream = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );
            await content.CopyToAsync(fileStream).ConfigureAwait(false);

            return newFileName;
        }

        /// <summary>
        /// Given the "key" (relative path with forward slashes, as returned by <see cref="UploadFileAsync"/>),
        /// loads the file from disk, base64-encodes its content, and returns
        /// "filename={key};base64,{base64Payload}" — matching the format used by S3/Azure.
        /// </summary>
        public async Task<string?> GetFileDataAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("key cannot be null or empty", nameof(key));

            string fullPath = Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"No file found for key '{key}' (expected path: '{fullPath}')");

            byte[] allBytes = await File.ReadAllBytesAsync(fullPath).ConfigureAwait(false);
            string base64 = Convert.ToBase64String(allBytes);

            return $"filename={key};base64,{base64}";
        }

        /// <summary>
        /// Deletes every file under "{_rootPath}/{objectType}/{objectProperty}/{objectId}/"
        /// except the one matching <paramref name="activeBlobName"/>. No-op when
        /// <paramref name="objectId"/> is the staging placeholder ("0" or empty) — staged
        /// uploads live under a separate "_tmp/" prefix and are pruned by the provider's
        /// lifecycle rule, not by this method.
        /// </summary>
        public Task DeleteNonActiveBlobs(
            string activeBlobName,
            string objectType,
            string objectProperty,
            string objectId
        )
        {

            if (BlobKeyConventions.IsStagingObjectId(objectId))
                return Task.CompletedTask;

            string entityDir = Path.Combine(_rootPath, objectType, objectProperty, objectId);

            if (!Directory.Exists(entityDir))
                return Task.CompletedTask;

            string? activeFullPath = string.IsNullOrEmpty(activeBlobName)
                ? null
                : Path.Combine(_rootPath, activeBlobName.Replace('/', Path.DirectorySeparatorChar));

            foreach (string fullPath in Directory.EnumerateFiles(entityDir))
            {
                if (activeFullPath != null && string.Equals(fullPath, activeFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete blob file: {FilePath}", fullPath);
                }
            }

            return Task.CompletedTask;
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
            if (!BlobKeyConventions.TryBuildPromotedKey(currentKey, objectType, objectProperty, objectId, out string? newKey))
                return currentKey;

            string sourcePath = Path.Combine(_rootPath, currentKey.Replace('/', Path.DirectorySeparatorChar));
            string destPath = Path.Combine(_rootPath, newKey.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            await Task.Run(() => File.Move(sourcePath, destPath));

            return newKey;
        }
    }
}
