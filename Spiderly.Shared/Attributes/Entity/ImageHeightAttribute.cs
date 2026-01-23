using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Validates exact image height for a blob property.
    /// This attribute provides both <i>server-side</i> and <i>client-side</i> validation.
    /// Use this attribute alongside <see cref="BlobNameAttribute"/> for image uploads.
    /// <br/><br/>
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [BlobName]
    ///     [ImageWidth(100)]
    ///     [ImageHeight(100)]
    ///     [StringLength(80, MinimumLength = 30)]
    ///     public string Avatar { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ImageHeightAttribute : Attribute
    {
        public int Height { get; }

        public ImageHeightAttribute(int height)
        {
            Height = height;
        }
    }
}
