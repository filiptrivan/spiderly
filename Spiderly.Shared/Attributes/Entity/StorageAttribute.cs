using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Abstract base for per-property storage selectors. A concrete subclass
    /// passes the implementation type of an <see cref="Spiderly.Shared.Interfaces.IFileManager"/>
    /// adapter to the base constructor; the presence of the subclass on a string property marks
    /// it as a blob (replacing the legacy <c>[BlobName]</c> marker) and tells the source generator
    /// which adapter to resolve from DI for upload/delete operations on that property. <br/> <br/>
    ///
    /// Spiderly ships three built-in subclasses: <c>[DiskStorage]</c>, <c>[S3PublicStorage]</c>,
    /// <c>[S3PrivateStorage]</c>. Each is detected by its concrete attribute name and dispatched
    /// to the matching `IFileManager` field in the generated entity service. Custom subclasses are
    /// recognized as blobs by the convention "attribute simple name ends with <c>Storage</c>", but
    /// the generator's auto-CRUD field-name resolution is currently hard-coded for the three
    /// built-ins — custom adapters integrate via direct injection in hand-written services. <br/> <br/>
    ///
    /// <b>Example — built-in:</b>
    /// <code>
    /// public class Brand : BusinessObject&lt;int&gt;
    /// {
    ///     [S3PublicStorage]
    ///     [AcceptedFileTypes("image/*")]
    ///     [StringLength(1000, MinimumLength = 1)]
    ///     public string LogoUrl { get; set; }
    /// }
    /// </code>
    ///
    /// <b>Example — custom adapter:</b>
    /// <code>
    /// public sealed class BackblazeStorageAttribute : StorageAttribute
    /// {
    ///     public BackblazeStorageAttribute() : base(typeof(BackblazeStorageService)) { }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public abstract class StorageAttribute : Attribute
    {
        /// <summary>
        /// The concrete <see cref="Spiderly.Shared.Interfaces.IFileManager"/> implementation this
        /// storage attribute represents. Available at runtime to consumers that want to discover
        /// which adapter a property is bound to (e.g. for diagnostics or custom upload paths).
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Overrides the storage key prefix for this property — the leading path of every
        /// uploaded blob's key (and therefore of its public URL for public providers). Defaults
        /// to <c>{EntityName}/{PropertyName}</c> (editor-image properties:
        /// <c>{EntityName}/{PropertyName}Image</c>); set it to a short lowercase path like
        /// <c>"products"</c> to get human/SEO-readable URLs such as
        /// <c>…/products/84512/cordless-drill-3f9a21c4.webp</c>. The prefix is the listing scope
        /// for blob cleanup and staging promotion, so it must be unique per blob property and no
        /// prefix may be a path-parent of another — both enforced at build time.
        /// </summary>
        public string? KeyPrefix { get; set; }

        protected StorageAttribute(Type serviceType)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        }
    }
}
