using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies the accepted file types for a blob property. Mandatory on every
    /// blob property (build error <c>SPIDERLY014</c> when missing) alongside any
    /// <see cref="StorageAttribute"/> subclass (e.g. <c>[S3PublicStorage]</c>).
    /// <br/><br/>
    /// Entry semantics:
    /// <br/>— MIME entries (<c>"image/png"</c>) are enforced server-side (declared type whitelist +
    /// content inspection) and passed to the UI file picker.
    /// <br/>— Type wildcards (<c>"image/*"</c>) match any declared type with that prefix.
    /// <br/>— Extension entries (<c>".pdf"</c> — leading dot, no '/') only widen the UI file picker;
    /// the server validates the MIME entries, so always pair an extension with its MIME type.
    /// <br/>— <c>"image/svg+xml"</c> is validated structurally (XML with an <c>&lt;svg&gt;</c> root,
    /// active content rejected) since SVG has no magic bytes, and is uploaded as-is (no ImageSharp
    /// optimization).
    /// <br/><br/>
    /// <b>Example:</b>
    /// <code>
    /// public class Catalog : BusinessObject&lt;int&gt;
    /// {
    ///     [S3PublicStorage]
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
