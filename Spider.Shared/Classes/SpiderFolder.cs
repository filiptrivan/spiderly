using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spider.Shared.Classes
{
    public class SpiderFolder
    {
        public string Name { get; set; }
        public List<SpiderFolder> ChildFolders { get; set; } = new();
        public List<SpiderFile> Files { get; set; } = new();
    }
}
