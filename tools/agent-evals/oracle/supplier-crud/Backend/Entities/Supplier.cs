using System.ComponentModel.DataAnnotations;

namespace Backend.Entities;

// Oracle (known-good) solution for the supplier-crud task on the plain-.NET baseline. Doubles as
// living documentation of the intended plain-arm solution.
public class Supplier
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
