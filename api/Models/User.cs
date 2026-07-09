using System.ComponentModel.DataAnnotations;

namespace PremierClic.Api.Models;

public class User
{
    public Guid Id { get; set; }
    [Required]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
