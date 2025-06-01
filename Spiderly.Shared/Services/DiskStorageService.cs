using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Services
{
    public class DiskStorageService : IFileManager
    {
        private readonly string _rootPath;

        /// <summary>
        /// By default, files will be stored in:
        ///   {CurrentDirectory}/FileStorage
        /// </summary>
        public DiskStorageService()
            : this(Path.Combine(Directory.GetCurrentDirectory(), "FileStorage"))
        {
        }

        /// <summary>
        /// If you want to store files under a custom root, pass it in here.
        /// </summary>
        public DiskStorageService(string rootPath)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
        }

        /// <summary>
        /// 1. Extracts the extension from the original fileName.
        /// 2. Builds a new filename: 
        ///      "{objectType}-{objectProperty}-{objectId}-{GUID}.{extension}"
        /// 3. Saves the stream at "{_rootPath}/{newFilename}".
        /// 4. Returns just "newFilename" (this is your "key").
        /// </summary>
        public async Task<string> UploadFileAsync(
            string fileName,
            string objectType,
            string objectProperty,
            string objectId,
            Stream content
        )
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("fileName cannot be null or empty", nameof(fileName));
            if (string.IsNullOrWhiteSpace(objectType))
                throw new ArgumentException("objectType cannot be null or empty", nameof(objectType));
            if (string.IsNullOrWhiteSpace(objectProperty))
                throw new ArgumentException("objectProperty cannot be null or empty", nameof(objectProperty));
            if (string.IsNullOrWhiteSpace(objectId))
                throw new ArgumentException("objectId cannot be null or empty", nameof(objectId));
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            string fileExtension = Helper.GetFileExtensionFromFileName(fileName);
            if (string.IsNullOrEmpty(fileExtension))
                throw new ArgumentException("Cannot determine file extension from: " + fileName, nameof(fileName));

            string newFileName = $"{objectId}-{objectType}-{objectProperty}-{Guid.NewGuid()}.{fileExtension}"; // e.g. "1234-User-LogoBlobName-9f1b2c3d4e5f6789abcdef.pdf"
            string fullPath = Path.Combine(_rootPath, newFileName);

            // Write the Stream to disk under "{_rootPath}/{newFileName}"
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
        /// Given the "key",
        /// 1. Loads "{_rootPath}/{key}" into memory,
        /// 2. Base64‐encodes it,
        /// 3. Returns "filename={key};base64,{base64Payload}".
        /// </summary>
        public async Task<string> GetFileDataAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("key cannot be null or empty", nameof(key));

            string fullPath = Path.Combine(_rootPath, key);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"No file found for key '{key}' (expected path: '{fullPath}')");

            byte[] allBytes = await File.ReadAllBytesAsync(fullPath).ConfigureAwait(false);
            string base64 = Convert.ToBase64String(allBytes);

            return $"filename={key};base64,{base64}";
        }

        /// <summary>
        /// Deletes every file under {_rootPath} whose filename 
        /// starts with "{objectId}-{objectType}-{objectProperty}-"
        /// except the one named exactly "activeBlobName".
        /// 
        /// If objectId == "0", it does nothing (to avoid accidental mass‐deletion).
        /// </summary>
        public Task DeleteNonActiveBlobs(
            string activeBlobName,
            string objectType,
            string objectProperty,
            string objectId
        )
        {
            if (string.IsNullOrWhiteSpace(objectType))
                throw new ArgumentException("objectType cannot be null or empty", nameof(objectType));
            if (string.IsNullOrWhiteSpace(objectProperty))
                throw new ArgumentException("objectProperty cannot be null or empty", nameof(objectProperty));
            if (string.IsNullOrWhiteSpace(objectId))
                throw new ArgumentException("objectId cannot be null or empty", nameof(objectId));

            if (objectId == "0") // If objectId is "0", we skip deletion entirely
                return Task.CompletedTask;

            // Build the prefix to match, e.g. "User-Logo-1234-"
            string prefix = $"{objectId}-{objectType}-{objectProperty}-";

            // Enumerate every file in _rootPath:
            foreach (string fullPath in Directory.EnumerateFiles(_rootPath))
            {
                string fileName = Path.GetFileName(fullPath);

                // If it matches the prefix but is NOT the active one, delete it:
                if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(fileName, activeBlobName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(fullPath);
                    }
                    catch
                    {
                        // In production, you might log or rethrow. 
                        // Here we swallow exceptions so other files can still be processed.
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
