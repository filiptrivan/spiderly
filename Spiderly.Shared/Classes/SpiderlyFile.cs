using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Classes
{
    public class SpiderlyFile
    {
        public string Name { get; set; } = null!; // Always set by the generator constructing the file tree
        public string Data { get; set; } = null!; // Always set by the generator constructing the file tree
    }
}
