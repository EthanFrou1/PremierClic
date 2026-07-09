namespace PremierClic.Api.Models;

public class EmailTemplate
{
    public Guid Id { get; set; }
    public string? Nom { get; set; }
    public string? Sujet { get; set; }
    public string? CorpsHtml { get; set; }
}
