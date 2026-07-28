using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class UITableColumn
    {
        public string TranslationKey { get; set; } = null!;
        public string Field { get; set; } = null!;
    }
}
