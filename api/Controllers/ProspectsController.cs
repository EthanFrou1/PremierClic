using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierClic.Api.Data;
using PremierClic.Api.Models;

namespace PremierClic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProspectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProspectsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? statut, [FromQuery] string? categorie, [FromQuery] string? ville)
    {
        var q = _db.Prospects.AsQueryable();
        if (!string.IsNullOrEmpty(statut) && Enum.TryParse<ProspectStatus>(statut, out var s)) q = q.Where(p => p.Statut == s);
        if (!string.IsNullOrEmpty(categorie)) q = q.Where(p => p.Categorie == categorie);
        if (!string.IsNullOrEmpty(ville)) q = q.Where(p => p.Ville == ville);
        var list = await q.ToListAsync();

        var deployedByProspect = await _db.Mockups
            .GroupBy(m => m.ProspectId)
            .Select(g => new { ProspectId = g.Key, HasDeployedMockup = g.Any(m => m.UrlPreview != null) })
            .ToDictionaryAsync(x => x.ProspectId, x => x.HasDeployedMockup);

        var result = list.Select(p => new
        {
            p.Id,
            p.Nom,
            p.Categorie,
            p.Adresse,
            p.Ville,
            p.CodePostal,
            p.Telephone,
            p.Email,
            p.SourceDonnees,
            p.GooglePlaceId,
            p.ADejaUnSiteWeb,
            p.Statut,
            p.Notes,
            p.DateCreation,
            p.DateDerniereMaj,
            HasMockup = deployedByProspect.ContainsKey(p.Id),
            HasDeployedMockup = deployedByProspect.TryGetValue(p.Id, out var deployed) && deployed
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var p = await _db.Prospects.FindAsync(id);
        if (p == null) return NotFound();
        return Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Prospect prospect)
    {
        prospect.Id = Guid.NewGuid();
        prospect.DateCreation = DateTime.UtcNow;
        prospect.DateDerniereMaj = DateTime.UtcNow;
        _db.Prospects.Add(prospect);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = prospect.Id }, prospect);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Prospect updated)
    {
        var p = await _db.Prospects.FindAsync(id);
        if (p == null) return NotFound();
        p.Nom = updated.Nom;
        p.Categorie = updated.Categorie;
        p.Adresse = updated.Adresse;
        p.Ville = updated.Ville;
        p.CodePostal = updated.CodePostal;
        p.Telephone = updated.Telephone;
        p.Email = updated.Email;
        p.SourceDonnees = updated.SourceDonnees;
        p.ADejaUnSiteWeb = updated.ADejaUnSiteWeb;
        p.Statut = updated.Statut;
        p.Notes = updated.Notes;
        p.DateDerniereMaj = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var p = await _db.Prospects.FindAsync(id);
        if (p == null) return NotFound();
        _db.Prospects.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import()
    {
        List<ProspectImportDto> entries;

        if (Request.HasFormContentType && Request.Form.Files.Count > 0)
        {
            var file = Request.Form.Files[0];
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            entries = ParseCsv(text);
        }
        else
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            entries = JsonSerializer.Deserialize<List<ProspectImportDto>>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ProspectImportDto>();
        }

        var created = new List<Prospect>();
        foreach (var item in entries)
        {
            var prospect = new Prospect
            {
                Id = Guid.NewGuid(),
                Nom = item.Nom ?? string.Empty,
                Categorie = item.Categorie,
                Adresse = item.Adresse,
                Ville = item.Ville,
                CodePostal = item.CodePostal,
                Telephone = item.Telephone,
                Email = item.Email,
                SourceDonnees = item.SourceDonnees,
                ADejaUnSiteWeb = item.ADejaUnSiteWeb,
                Statut = ParseStatut(item.Statut),
                Notes = item.Notes,
                DateCreation = DateTime.UtcNow,
                DateDerniereMaj = DateTime.UtcNow
            };
            _db.Prospects.Add(prospect);
            created.Add(prospect);
        }

        await _db.SaveChangesAsync();
        return Ok(new { imported = created.Count, prospects = created.Select(p => new { p.Id, p.Nom, p.Ville, p.Categorie, p.Statut }) });
    }

    private static List<ProspectImportDto> ParseCsv(string csvText)
    {
        var lines = csvText.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return new List<ProspectImportDto>();

        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        var entries = new List<ProspectImportDto>();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length == 0) continue;
            var dto = new ProspectImportDto();
            for (var j = 0; j < headers.Length && j < values.Length; j++)
            {
                var header = headers[j].ToLowerInvariant();
                var value = values[j].Trim();
                switch (header)
                {
                    case "nom": dto.Nom = value; break;
                    case "categorie": dto.Categorie = value; break;
                    case "adresse": dto.Adresse = value; break;
                    case "ville": dto.Ville = value; break;
                    case "codepostal": dto.CodePostal = value; break;
                    case "telephone": dto.Telephone = value; break;
                    case "email": dto.Email = value; break;
                    case "sourcedonnees": dto.SourceDonnees = value; break;
                    case "adejaunsiteweb": dto.ADejaUnSiteWeb = value == "true" || value == "1"; break;
                    case "statut": dto.Statut = value; break;
                    case "notes": dto.Notes = value; break;
                }
            }
            entries.Add(dto);
        }

        return entries;
    }

    private static ProspectStatus ParseStatut(string? statut)
    {
        if (!string.IsNullOrEmpty(statut) && Enum.TryParse<ProspectStatus>(statut.Replace(" ", string.Empty), true, out var parsed))
        {
            return parsed;
        }
        return ProspectStatus.ANouveauFait;
    }
}

public class ProspectImportDto
{
    public string? Nom { get; set; }
    public string? Categorie { get; set; }
    public string? Adresse { get; set; }
    public string? Ville { get; set; }
    public string? CodePostal { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? SourceDonnees { get; set; }
    public bool ADejaUnSiteWeb { get; set; }
    public string? Statut { get; set; }
    public string? Notes { get; set; }
}
