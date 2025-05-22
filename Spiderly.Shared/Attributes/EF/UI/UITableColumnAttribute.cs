using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// <b>Usage:</b> Specifies which columns should be displayed in a table view for a <i>many-to-many</i> relationship.
    /// Must be used in combination with <i>[SimpleManyToManyTableLazyLoad]</i> attribute. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class Partner : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName]
    ///     public string Name { get; set; }
    ///     
    ///     #region UITableColumn
    ///     [UITableColumn(nameof(PartnerUserDTO.UserDisplayName))]
    ///     [UITableColumn(nameof(PartnerUserDTO.Points))]
    ///     [UITableColumn(nameof(PartnerUserDTO.TierDisplayName))]
    ///     [UITableColumn(nameof(PartnerUserDTO.CheckedSegmentationItemsCommaSeparated), "Segmentation")] // Custom translation key
    ///     [UITableColumn(nameof(PartnerUserDTO.CreatedAt))]
    ///     #endregion
    ///     [SimpleManyToManyTableLazyLoad]
    ///     public virtual List&lt;PartnerUser&gt; Recipients { get; set; } = new(); // M2M relationship
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class UITableColumnAttribute : Attribute
    {
        /// <param name="field">DTO property name (e.g. nameof(PartnerUserDTO.UserDisplayName))</param>
        /// <param name="translateKey">If DTO property name and translate key from en.json are compatible you don't need to pass anything</param>
        public UITableColumnAttribute(string field, string translateKey = null) { }
    }
}
