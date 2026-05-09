using System;
using Spiderly.Shared.Services;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Routes upload/delete operations for the decorated string property through
    /// <see cref="S3PublicStorageService"/>. The bucket is configured for public access and
    /// the column stores a fully-qualified CDN URL; objects are uploaded with
    /// <c>Cache-Control: public, max-age=31536000, immutable</c>. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class Brand : BusinessObject&lt;int&gt;
    /// {
    ///     [S3PublicStorage]
    ///     [AcceptedFileTypes("image/*")]
    ///     [StringLength(1000, MinimumLength = 1)]
    ///     public string LogoUrl { get; set; }
    /// }
    /// </code>
    /// </summary>
    public sealed class S3PublicStorageAttribute : StorageAttribute
    {
        public S3PublicStorageAttribute() : base(typeof(S3PublicStorageService)) { }
    }
}
