using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Marks a string property as a reference to a file stored in Azure Blob Storage.
    /// This property will contain the unique identifier (blob name) used to locate
    /// and access the file in Azure Storage.
    /// </summary>
    /// <remarks>
    /// <b>Example:</b> <br/>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [BlobName]
    ///     public string ProfilePictureBlobName { get; set; }
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class BlobNameAttribute : Attribute
    {
    }
}
