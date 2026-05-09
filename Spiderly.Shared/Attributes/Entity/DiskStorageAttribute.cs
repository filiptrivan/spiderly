using System;
using Spiderly.Shared.Services;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Routes upload/delete operations for the decorated string property through
    /// <see cref="DiskStorageService"/>, storing files under the local filesystem. Intended for
    /// local development; not recommended for production deployments where the host filesystem
    /// is ephemeral or shared across replicas. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [DiskStorage]
    ///     [AcceptedFileTypes("image/*")]
    ///     [StringLength(1000, MinimumLength = 1)]
    ///     public string ProfilePicture { get; set; }
    /// }
    /// </code>
    /// </summary>
    public sealed class DiskStorageAttribute : StorageAttribute
    {
        public DiskStorageAttribute() : base(typeof(DiskStorageService)) { }
    }
}
