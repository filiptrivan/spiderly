using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// Set to the enumerable property in combination with `SimpleManyToManyTableLazyLoad` attribute.
    /// e.g.
    /// ```csharp
    /// #region UITableColumn
    /// [UITableColumn(nameof(PartnerUserDTO.UserDisplayName))]
    /// [UITableColumn(nameof(PartnerUserDTO.Points))]
    /// [UITableColumn(nameof(PartnerUserDTO.TierDisplayName))]
    /// [UITableColumn(nameof(PartnerUserDTO.CheckedSegmentationItemsCommaSeparated), "Segmentation")]
    /// [UITableColumn(nameof(PartnerUserDTO.CreatedAt))]
    /// #endregion
    /// [SimpleManyToManyTableLazyLoad]
    /// public virtual List<PartnerUser> Recipients { get; } = new(); // M2M
    /// ```
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class UITableColumnAttribute : Attribute
    {
        /// <param name="field">DTO property name (e.g. nameof(PartnerUserDTO.UserDisplayName))</param>
        /// <param name="translateKey">If DTO property name and translate key from en.json are compatible you don't need to pass anything</param>
        public UITableColumnAttribute(string field, string translateKey = null) { }
    }
}
