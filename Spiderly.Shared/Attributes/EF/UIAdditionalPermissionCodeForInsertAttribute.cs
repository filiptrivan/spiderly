using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies additional permission requirements for inserting entities in the UI.
    /// This attribute allows defining extra permission checks beyond the default insert permissions.
    /// </summary>
    /// <remarks>
    /// Multiple instances of this attribute can be applied to a single entity.
    /// The user must have ONE of the specified permissions to perform the insert operation.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class UIAdditionalPermissionCodeForInsertAttribute : Attribute
    {
        public UIAdditionalPermissionCodeForInsertAttribute(string permissionCode) { }
    }
}
