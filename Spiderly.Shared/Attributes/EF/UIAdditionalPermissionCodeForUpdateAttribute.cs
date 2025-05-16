using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Specifies additional permission requirements for updating entities in the UI.
    /// The user must have ONE of the specified permissions to perform the update operation.
    /// Multiple instances of this attribute can be applied to a single entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class UIAdditionalPermissionCodeForUpdateAttribute : Attribute
    {
        public UIAdditionalPermissionCodeForUpdateAttribute(string permissionCode) { }
    }
}
