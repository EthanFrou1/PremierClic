using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierClic.Api.Data;
using PremierClic.Api.Models;

namespace PremierClic.Api.Controllers;

[ApiController]
[Route("api/prospects/{prospectId}/[controller]")]
public class ProspectMessagesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProspectMessagesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid prospectId)
    {
        var messages = await _db.ProspectMessages
            .Where(m => m.ProspectId == prospectId)
            .OrderByDescending(m => m.DateCreation)
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid prospectId, [FromBody] AddProspectMessageRequest request)
    {
        if (!await _db.Prospects.AnyAsync(p => p.Id == prospectId)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Le message est requis.");

        var message = new ProspectMessage
        {
            Id = Guid.NewGuid(),
            ProspectId = prospectId,
            Canal = request.Canal,
            Prompt = request.Prompt ?? string.Empty,
            Message = request.Message.Trim(),
            DateCreation = DateTime.UtcNow
        };
        _db.ProspectMessages.Add(message);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { prospectId }, message);
    }

    [HttpDelete("{messageId}")]
    public async Task<IActionResult> Delete(Guid prospectId, Guid messageId)
    {
        var message = await _db.ProspectMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.ProspectId == prospectId);
        if (message == null) return NotFound();

        _db.ProspectMessages.Remove(message);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }
}

public class AddProspectMessageRequest
{
    public string? Canal { get; set; }
    public string? Prompt { get; set; }
    public string Message { get; set; } = string.Empty;
}
