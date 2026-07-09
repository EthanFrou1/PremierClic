namespace PremierClic.Api.Models;

public class Mockup
{
    public Guid Id { get; set; }
    public Guid ProspectId { get; set; }
    public string? UrlPreview { get; set; }
    public string? Path { get; set; }
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public string? Commentaire { get; set; }
}
