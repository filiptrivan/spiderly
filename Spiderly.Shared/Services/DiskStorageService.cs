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
        public Task DeleteNonActiveBlobs(string activeBlobName, string objectType, string objectProperty, string objectId)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFileDataAsync(string key)
        {
            throw new NotImplementedException();
        }

        public Task<string> UploadFileAsync(string fileName, string objectType, string objectProperty, string objectId, Stream content)
        {
            throw new NotImplementedException();
        }
    }
}
