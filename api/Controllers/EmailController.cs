using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PremierClic.Api.Data;
using PremierClic.Api.Models;

namespace PremierClic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public EmailController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _db.EmailTemplates.ToListAsync();
        return Ok(templates);
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] EmailTemplate template)
    {
        template.Id = Guid.NewGuid();
        _db.EmailTemplates.Add(template);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTemplates), new { id = template.Id }, template);
    }

    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare([FromBody] SendEmailDto dto)
    {
        var prospect = await _db.Prospects.FindAsync(dto.ProspectId);
        if (prospect == null) return NotFound("Prospect introuvable");

        var (subject, body, _) = await PrepareEmailAsync(prospect, dto);
        return Ok(new { subject, bodyHtml = body });
    }

    [HttpPost("mark-sent")]
    public async Task<IActionResult> MarkSent([FromBody] SendEmailDto dto)
    {
        var prospect = await _db.Prospects.FindAsync(dto.ProspectId);
        if (prospect == null) return NotFound("Prospect introuvable");

        var (subject, body, token) = await PrepareEmailAsync(prospect, dto);

        var record = new EmailEnvoye
        {
            Id = Guid.NewGuid(),
            ProspectId = prospect.Id,
            Sujet = subject,
            CorpsHtml = body,
            Statut = EmailStatut.Envoye,
            DateEnvoi = DateTime.UtcNow,
            TokenDesinscription = token
        };

        _db.EmailEnvoyes.Add(record);
        await _db.SaveChangesAsync();

        return Ok(new { marked = true });
    }

    private async Task<(string subject, string body, string token)> PrepareEmailAsync(Prospect prospect, SendEmailDto dto)
    {
        var template = dto.TemplateId.HasValue
            ? await _db.EmailTemplates.FindAsync(dto.TemplateId.Value)
            : null;

        var subject = dto.Subject ?? template?.Sujet ?? "Première prise de contact";
        var body = dto.BodyHtml ?? template?.CorpsHtml ?? string.Empty;

        var token = prospect.TokenDesinscription;
        if (string.IsNullOrEmpty(token))
        {
            token = Guid.NewGuid().ToString();
            prospect.TokenDesinscription = token;
            await _db.SaveChangesAsync();
        }

        var unsubscribeUrl = BuildUnsubscribeUrl(token);
        subject = subject.Replace("{{nom}}", prospect.Nom)
                          .Replace("{{email}}", prospect.Email ?? string.Empty)
                          .Replace("{{unsubscribeUrl}}", unsubscribeUrl);
        body = body.Replace("{{nom}}", prospect.Nom)
                   .Replace("{{email}}", prospect.Email ?? string.Empty)
                   .Replace("{{unsubscribeUrl}}", unsubscribeUrl);

        return (subject, body, token);
    }

    [HttpGet("unsubscribe/{token}")]
    public async Task<IActionResult> Unsubscribe(string token)
    {
        var prospect = await _db.Prospects.FirstOrDefaultAsync(p => p.TokenDesinscription == token);
        if (prospect == null) return NotFound();
        prospect.Statut = ProspectStatus.DesinscritOptOut;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Vous êtes désinscrit des futures campagnes." });
    }

    private string BuildUnsubscribeUrl(string token)
    {
        var origin = _configuration["FRONTEND_ORIGIN"] ?? "http://localhost:5173";
        return $"{origin}/unsubscribe/{token}";
    }
}

public class SendEmailDto
{
    public Guid ProspectId { get; set; }
    public Guid? TemplateId { get; set; }
    public string? Subject { get; set; }
    public string? BodyHtml { get; set; }
}
