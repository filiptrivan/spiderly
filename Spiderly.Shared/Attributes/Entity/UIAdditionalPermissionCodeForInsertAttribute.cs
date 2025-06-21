using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies additional permission requirements for inserting entities in the UI.
    /// The user must have ONE of the specified permissions to perform the insert operation.
    /// Multiple instances of this attribute can be applied to a single entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class UIAdditionalPermissionCodeForInsertAttribute : Attribute
    {
        public UIAdditionalPermissionCodeForInsertAttribute(string permissionCode) { }
    }
}
