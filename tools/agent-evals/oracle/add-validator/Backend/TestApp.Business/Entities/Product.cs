using System.ComponentModel.DataAnnotations;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;

namespace TestApp.Business.Entities
{
    // Known-good solution for the `add-validator` task: Product.Name is now required + max 100 via
    // DataAnnotations (Spiderly generates the FluentValidation + Angular validators from these).
    // Overlaid verbatim onto the staged fixture (oracle.mjs copies, no sed), so the namespace is the
    // concrete TEST_APP_NAME — not the __APP_NAME__ placeholder the pre-task fixture uses.
    [SpiderlyEntity]
    [DoNotAuthorize]
    public class Product : BusinessObject<int>
    {
        [DisplayName]
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }
    }
}
