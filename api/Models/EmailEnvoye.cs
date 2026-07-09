namespace PremierClic.Api.Models;

public enum EmailStatut { Envoye, Ouvert, Repondu, Bounced }

public class EmailEnvoye
{
    public Guid Id { get; set; }
    public Guid ProspectId { get; set; }
    public string? Sujet { get; set; }
    public string? CorpsHtml { get; set; }
    public DateTime DateEnvoi { get; set; } = DateTime.UtcNow;
    public EmailStatut Statut { get; set; } = EmailStatut.Envoye;
    public string? TokenDesinscription { get; set; }
}
