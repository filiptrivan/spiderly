using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace __APP_NAME__.Business.Entities
{
    /// <summary>
    /// Dependent side of an optional one-to-one with <see cref="Project"/> — exercises native [WithOne]
    /// support end-to-end in the e2e suite: the autocomplete UI on the dependent's own page, the auto
    /// unique index (multiple un-chartered rows must coexist — NULLS DISTINCT on Postgres), app-layer
    /// cascade (deleting a Project deletes its charter), and the duplicate-charter 409.
    /// The FK lives here; <see cref="Project.ProjectCharter"/> is the principal inverse.
    /// </summary>
    [SpiderlyEntity]
    public class ProjectCharter : BusinessObject<long>
    {
        [DisplayName]
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; }

        [UIControlType(nameof(UIControlTypeCodes.TextArea))]
        [StringLength(2000, MinimumLength = 1)]
        public string Scope { get; set; }

        // Optional explicit FK — nullable so most projects have no charter and many NULLs must be allowed.
        public long? CharteredProjectId { get; set; }

        [WithOne(nameof(Project.ProjectCharter))]
        [CascadeDelete]
        public virtual Project CharteredProject { get; set; }
    }
}
