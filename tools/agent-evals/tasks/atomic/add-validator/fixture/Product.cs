using System.ComponentModel.DataAnnotations;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;

namespace __APP_NAME__.Business.Entities
{
    // Eval PRE-task state for the `add-validator` task. Product.Name intentionally has NO
    // validation — the agent's job is to make it required + max 100 using Spiderly's conventions.
    // Do NOT add [Required]/[MaxLength] here, or the task becomes a no-op (a do-nothing agent would
    // then pass the verifier). See ../verify.mjs.
    [SpiderlyEntity]
    [DoNotAuthorize]
    public class Product : BusinessObject<int>
    {
        [DisplayName]
        public string Name { get; set; }
    }
}
