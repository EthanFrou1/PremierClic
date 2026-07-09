using System.ComponentModel.DataAnnotations;

namespace PremierClic.Api.Models;

public enum ProspectStatus
{
    ANouveauFait,
    AContacter,
    Contacte,
    Relance,
    Interessé,
    PasInteresse,
    Client,
    DesinscritOptOut
}

public class Prospect
{
    public Guid Id { get; set; }
    [Required]
    public string Nom { get; set; } = string.Empty;
    public string? Categorie { get; set; }
    public string? Adresse { get; set; }
    public string? Ville { get; set; }
    public string? CodePostal { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? SourceDonnees { get; set; }
    public string? GooglePlaceId { get; set; }
    public bool ADejaUnSiteWeb { get; set; }
    public ProspectStatus Statut { get; set; } = ProspectStatus.ANouveauFait;
    public string? Notes { get; set; }
    public string? TokenDesinscription { get; set; }
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public DateTime DateDerniereMaj { get; set; } = DateTime.UtcNow;
}
