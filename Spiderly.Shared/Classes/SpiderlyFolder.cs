using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Classes
{
    public class SpiderlyFolder
    {
        public string Name { get; set; }
        public List<SpiderlyFolder> ChildFolders { get; set; } = new();
        public List<SpiderlyFile> Files { get; set; } = new();
    }
}
