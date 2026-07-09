namespace PremierClic.Api.Models;

public class ProspectMessage
{
    public Guid Id { get; set; }
    public Guid ProspectId { get; set; }
    public string? Canal { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}
