using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderValidationRule
    {
        public SpiderlyProperty Property { get; set; }
        public List<SpiderValidationRulePart> ValidationRuleParts { get; set; } = new();
    }
}
