using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace Spiderly.Security.Entities
{
    [Index(nameof(Code), IsUnique = true)]
    [UIDoNotGenerate]
    public class Permission : ReadonlyObject<int>
    {
        [DisplayName]
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string NameLatin { get; set; }

        [StringLength(400, MinimumLength = 1)]
        public string Description { get; set; }

        [StringLength(400, MinimumLength = 1)]
        public string DescriptionLatin { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Code { get; set; }

        public virtual List<Role> Roles { get; } = new();
    }
}
