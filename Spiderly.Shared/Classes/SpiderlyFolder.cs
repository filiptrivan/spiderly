using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Classes
{
    public class SpiderlyFolder
    {
        public string Name { get; set; } = null!; // Always set by the generator constructing the folder tree
        public List<SpiderlyFolder> ChildFolders { get; set; } = new();
        public List<SpiderlyFile> Files { get; set; } = new();
    }
}
