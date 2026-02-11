using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies the maximum allowed file size (in bytes) for a blob property.
    /// When not applied, the file upload defaults to 20 MB (20,000,000 bytes).
    /// Use this attribute alongside <see cref="BlobNameAttribute"/> for file uploads.
    /// <br/><br/>
    /// <b>Example:</b>
    /// <code>
    /// public class Catalog : BusinessObject&lt;int&gt;
    /// {
    ///     [BlobName]
    ///     [MaxFileSize(5_000_000)]
    ///     [AcceptedFileTypes("application/pdf", ".pdf")]
    ///     [StringLength(1000, MinimumLength = 1)]
    ///     public string File { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class MaxFileSizeAttribute : Attribute
    {
        public int MaxFileSize { get; }

        public MaxFileSizeAttribute(int maxFileSize)
        {
            MaxFileSize = maxFileSize;
        }
    }
}
