// using Spiderly.Shared.Attributes.Entity;
// using Spiderly.Shared.Attributes.Entity.UI;
// using Spiderly.Shared.BaseEntities;
// using Spiderly.Shared.Enums;
// using System.ComponentModel.DataAnnotations;

// namespace Spiderly.Security.Entities
// {
//     public class Role : BusinessObject<int>
//     {
//         [DisplayName]
//         [Required]
//         [StringLength(255, MinimumLength = 1)]
//         public string Name { get; set; }

//         [StringLength(400, MinimumLength = 1)]
//         public string Description { get; set; }

//         [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
//         public virtual List<Permission> Permissions { get; } = new();
//     }
// }