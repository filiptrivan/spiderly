using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace __APP_NAME__.Business.Entities
{
    [SpiderlyEntity]
    public class Project : BusinessObject<long>
    {
        [DisplayName]
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Name { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.TextArea))]
        [StringLength(1000, MinimumLength = 1)]
        public string Description { get; set; }

        [Required]
        [Precision(18, 2)]
        [GreaterThanOrEqualTo(0)]
        public decimal Budget { get; set; }

        [Required]
        [GreaterThanOrEqualTo(1)]
        public int MaxMembers { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.Calendar))]
        public DateTime? Deadline { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.Editor))]
        [StringLength(10000, MinimumLength = 1)]
        public string Documentation { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.Markdown))]
        [StringLength(10000, MinimumLength = 1)]
        public string Readme { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.CheckBox))]
        public bool? IsArchived { get; set; }

        [GenerateCommaSeparatedDisplayName]
        [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
        public virtual List<User> Members { get; } = new(); // M2M

        [UIOrderedOneToMany]
        public virtual List<ProjectTask> ProjectTasks { get; } = new();

        // Principal inverse of the optional one-to-one with ProjectCharter (the FK + [WithOne] live on
        // ProjectCharter). A plain single-valued nav with no attribute — the 1-1 principal-inverse rule
        // excludes it from Project's generated DTO/UI, so project-crud.spec.ts is unaffected.
        public virtual ProjectCharter ProjectCharter { get; set; }
    }
}
