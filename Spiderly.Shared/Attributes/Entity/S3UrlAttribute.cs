using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    [AttributeUsage(AttributeTargets.Property)]
    public class S3UrlAttribute : Attribute
    {
        public S3UrlAttribute() { }
    }
}
