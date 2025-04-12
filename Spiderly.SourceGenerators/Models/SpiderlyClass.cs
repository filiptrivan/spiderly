using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyClass
    {
        public string Name { get; set; }
        public string Namespace { get; set; }

        /// <summary>
        /// Here is only one base type, no interfaces
        /// </summary>
        public string BaseType { get; set; }

        public bool IsAbstract { get; set; }

        public string ControllerName { get; set; }

        /// <summary>
        /// For the DTO classes
        /// </summary>
        public bool IsGenerated { get; set; }

        public List<SpiderlyProperty> Properties { get; set; } = new();

        public List<SpiderlyAttribute> Attributes { get; set; } = new();

        public List<SpiderMethod> Methods { get; set; } = new();
    }
}
