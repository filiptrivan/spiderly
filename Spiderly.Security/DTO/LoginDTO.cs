using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.DTO;
using System.ComponentModel.DataAnnotations;

namespace Spiderly.Security.DTO
{
    // FT: I think there is no need for any validation on BrowserId, the code will not brake, and we are not saving the data in the database
    [SpiderlyDTO]
    public partial class LoginDTO
    {
        [Required]
        [StringLength(100, MinimumLength = 5)]
        [Email]
        public string Email { get; set; } = null!;
        public string? BrowserId { get; set; }
    }
}
