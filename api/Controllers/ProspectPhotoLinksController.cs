using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierClic.Api.Data;
using PremierClic.Api.Models;

namespace PremierClic.Api.Controllers;

[ApiController]
[Route("api/prospects/{prospectId}/[controller]")]
public class ProspectPhotoLinksController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProspectPhotoLinksController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid prospectId)
    {
        var links = await _db.ProspectPhotoLinks
            .Where(p => p.ProspectId == prospectId)
            .OrderBy(p => p.DateAjout)
            .ToListAsync();

        return Ok(links);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid prospectId, [FromBody] AddPhotoLinkRequest request)
    {
        if (!await _db.Prospects.AnyAsync(p => p.Id == prospectId)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Url)) return BadRequest("Le lien est requis.");
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest("Le lien doit être une URL http(s) valide.");

        var link = new ProspectPhotoLink
        {
            Id = Guid.NewGuid(),
            ProspectId = prospectId,
            Url = request.Url.Trim(),
            DateAjout = DateTime.UtcNow
        };
        _db.ProspectPhotoLinks.Add(link);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { prospectId }, link);
    }

    [HttpDelete("{linkId}")]
    public async Task<IActionResult> Delete(Guid prospectId, Guid linkId)
    {
        var link = await _db.ProspectPhotoLinks.FirstOrDefaultAsync(p => p.Id == linkId && p.ProspectId == prospectId);
        if (link == null) return NotFound();

        _db.ProspectPhotoLinks.Remove(link);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }
}

public class AddPhotoLinkRequest
{
    public string Url { get; set; } = string.Empty;
}
