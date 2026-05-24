//HintName: DTOList.generated.cs
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.DTO;
using Spiderly.Security.DTO;
using Spiderly.Shared.Helpers;
using TestApp.Business.Enums;

namespace TestApp.Business.DTO
{
    [SpiderlyDTO]
    public partial class ArticleDTO : BusinessObjectDTO<long>
    {
        public string Title { get; set; }
    }

    [SpiderlyDTO]
    public partial class ArticleSaveBodyDTO 
    {
        public ArticleDTO ArticleDTO { get; set; }
        public List<long> SelectedKeptTagsIds { get; set; } = new();
        public List<NamebookDTO<long>> SelectedKeptContributorsNamebookDTOList { get; set; } = new();
    }

    [SpiderlyDTO]
    public partial class ArticleMainUIFormDTO 
    {
        public ArticleDTO ArticleDTO { get; set; }
        public List<long> KeptTagsIds { get; set; } = new();
        public List<NamebookDTO<long>> KeptContributorsNamebookDTOList { get; set; } = new();
    }

    [SpiderlyDTO]
    public partial class TagDTO : BusinessObjectDTO<long>
    {
        public string Name { get; set; }
    }

    [SpiderlyDTO]
    public partial class TagSaveBodyDTO 
    {
        public TagDTO TagDTO { get; set; }
    }

    [SpiderlyDTO]
    public partial class TagMainUIFormDTO 
    {
        public TagDTO TagDTO { get; set; }
    }
}