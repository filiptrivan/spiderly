using System;
using Spiderly.Shared.Services;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Routes upload/delete operations for the decorated string property through
    /// <see cref="S3PrivateStorageService"/>. The column stores an opaque S3 key; access is
    /// expected to be mediated via signed URLs or a backend proxy rather than direct CDN
    /// retrieval. Intended for files that contain personal or compliance-sensitive data
    /// (warranty receipts, ID documents, customer-uploaded invoices). <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class WarrantyRegistration : BusinessObject&lt;long&gt;
    /// {
    ///     [S3PrivateStorage]
    ///     [AcceptedFileTypes("image/jpeg", "image/png", "application/pdf")]
    ///     [StringLength(1000, MinimumLength = 1)]
    ///     public string ReceiptImageUrl { get; set; }
    /// }
    /// </code>
    /// </summary>
    public sealed class S3PrivateStorageAttribute : StorageAttribute
    {
        public S3PrivateStorageAttribute() : base(typeof(S3PrivateStorageService)) { }
    }
}
