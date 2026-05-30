using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Removes the decorated entity property from generated DTO classes and the corresponding generated API
    /// contract. Use it for internal, persistence-only, or server-managed values that should not be exposed
    /// to the generated client.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcludeFromDTOAttribute : Attribute
    {
    }
}
