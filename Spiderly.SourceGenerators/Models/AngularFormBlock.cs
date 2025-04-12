using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class AngularFormBlock
    {
        public string FormControlName { get; set; }
        public SpiderlyProperty Property { get; set; }
    }
}
