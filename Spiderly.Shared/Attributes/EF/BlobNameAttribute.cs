using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Marks a string property as a reference to a file stored in Azure Blob Storage.
    /// This property will contain the unique identifier (<i>blob name</i>) used to locate
    /// and access the file in Azure Storage. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [BlobName]
    ///     [StringLength(80, MinimumLength = 30)] // GUID length
    ///     public string ProfilePicture { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class BlobNameAttribute : Attribute
    {
    }
}
