using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Opt-out of save-time blob cleanup for a storage property. By default, saving
    /// an entity deletes every stored blob under the property's key prefix except the active one —
    /// correct when the entity is the only holder of the URL. Put this attribute on the property
    /// when copies of its URL outlive the entity's current value (order-line snapshots, emails,
    /// exports, third-party feeds): replacing the blob then keeps the old bytes, so historical
    /// references keep rendering. An orphaned blob costs fractions of a cent and is invisible; a
    /// deleted-but-referenced blob breaks a customer-facing record — when one side of the trade is
    /// cents and the other is broken history, don't optimise the cents. <br/> <br/>
    ///
    /// <b>Example:</b>
    /// <code>
    /// public class ProductMedia : BusinessObject&lt;long&gt;
    /// {
    ///     [S3PublicStorage(KeyPrefix = "products")]
    ///     [RetainReplacedBlobs] // OrderItem.ImageUrl snapshots this URL at checkout
    ///     [AcceptedFileTypes("image/*")]
    ///     [StringLength(1000, MinimumLength = 1)]
    ///     public string Url { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RetainReplacedBlobsAttribute : Attribute
    {
    }
}
