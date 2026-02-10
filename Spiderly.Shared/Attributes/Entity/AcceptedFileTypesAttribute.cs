using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies the accepted file types for a blob property.
    /// When not applied, the file upload defaults to accepting images only (<c>image/*</c>).
    /// Use this attribute alongside <see cref="BlobNameAttribute"/> for non-image uploads (e.g., PDFs, Excel files).
    /// <br/><br/>
    /// <b>Example:</b>
    /// <code>
    /// public class Catalog : BusinessObject&lt;int&gt;
    /// {
    ///     [BlobName]
    ///     [S3PublicUrl]
    ///     [AcceptedFileTypes("application/pdf", ".pdf")]
    ///     [StringLength(1000, MinimumLength = 1)]
    ///     public string File { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AcceptedFileTypesAttribute : Attribute
    {
        public string[] FileTypes { get; }

        public AcceptedFileTypesAttribute(params string[] fileTypes)
        {
            FileTypes = fileTypes;
        }
    }
}
