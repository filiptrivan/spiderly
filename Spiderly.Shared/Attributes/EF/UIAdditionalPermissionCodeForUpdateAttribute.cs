using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies additional permission requirements for updating entities in the UI.
    /// This attribute allows defining extra permission checks beyond the default update permissions.
    /// </summary>
    /// <remarks>
    /// Multiple instances of this attribute can be applied to a single entity.
    /// The user must have ONE of the specified permissions to perform the update operation.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class UIAdditionalPermissionCodeForUpdateAttribute : Attribute
    {
        public UIAdditionalPermissionCodeForUpdateAttribute(string permissionCode) { }
    }
}
