using System.ComponentModel.DataAnnotations;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;

namespace __APP_NAME__.Business.Entities
{
    [SpiderlyEntity]
    [DoNotAuthorize]
    public class Product : BusinessObject<int>
    {
        [DisplayName]
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.TextArea))]
        [MaxLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        public decimal Price { get; set; }

        [Range(0, 999999)]
        public int Stock { get; set; }

        public bool? IsActive { get; set; }

        [DiskStorage]
        [AcceptedFileTypes("video/mp4")]
        [StringLength(1000)]
        public string VideoUrl { get; set; }
    }
}
