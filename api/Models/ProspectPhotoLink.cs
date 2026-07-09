namespace PremierClic.Api.Models;

public class ProspectPhotoLink
{
    public Guid Id { get; set; }
    public Guid ProspectId { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime DateAjout { get; set; } = DateTime.UtcNow;
}
