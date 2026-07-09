using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PremierClic.Api.Data;
using PremierClic.Api.Models;

namespace PremierClic.Api.Controllers;

[ApiController]
[Route("api/prospects/{prospectId}/[controller]")]
public class MockupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MockupsController(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid prospectId)
    {
        var mockups = await _db.Mockups.Where(m => m.ProspectId == prospectId).ToListAsync();
        return Ok(mockups);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid prospectId, [FromBody] MockupDto dto)
    {
        if (!await _db.Prospects.AnyAsync(p => p.Id == prospectId)) return NotFound();

        var mockup = new Mockup
        {
            Id = Guid.NewGuid(),
            ProspectId = prospectId,
            UrlPreview = dto.UrlPreview,
            Commentaire = dto.Commentaire,
            DateCreation = DateTime.UtcNow,
            Path = null
        };

        _db.Mockups.Add(mockup);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { prospectId }, mockup);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(Guid prospectId, [FromForm] IFormFile file, [FromForm] string? commentaire)
    {
        if (!await _db.Prospects.AnyAsync(p => p.Id == prospectId)) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("File is required");

        var uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

        var filename = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var path = Path.Combine(uploads, filename);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream);

        var mockup = new Mockup
        {
            Id = Guid.NewGuid(),
            ProspectId = prospectId,
            UrlPreview = null,
            Commentaire = commentaire,
            DateCreation = DateTime.UtcNow,
            Path = filename
        };

        _db.Mockups.Add(mockup);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { prospectId }, mockup);
    }

    [HttpPost("deploy")]
    public async Task<IActionResult> Deploy(Guid prospectId, [FromForm] IFormFile file, [FromForm] string? commentaire)
    {
        if (!await _db.Prospects.AnyAsync(p => p.Id == prospectId)) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("Un fichier HTML est requis.");

        var apiToken = _configuration["NETLIFY_API_TOKEN"];
        var siteId = _configuration["NETLIFY_SITE_ID"];
        if (string.IsNullOrWhiteSpace(apiToken) || string.IsNullOrWhiteSpace(siteId))
            return BadRequest("Le déploiement Netlify n'est pas configuré (NETLIFY_API_TOKEN / NETLIFY_SITE_ID).");

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("index.html", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            await file.CopyToAsync(entryStream);
        }
        zipStream.Position = 0;

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.netlify.com/api/v1/sites/{siteId}/deploys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = new StreamContent(zipStream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, $"Échec du déploiement Netlify : {errorBody}");
        }

        var deploy = await response.Content.ReadFromJsonAsync<NetlifyDeployResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var deployUrl = deploy?.DeploySslUrl ?? deploy?.DeployUrl;
        if (string.IsNullOrWhiteSpace(deployUrl))
            return StatusCode(502, "Netlify n'a pas renvoyé d'URL de déploiement.");

        var mockup = new Mockup
        {
            Id = Guid.NewGuid(),
            ProspectId = prospectId,
            UrlPreview = deployUrl,
            Commentaire = commentaire,
            DateCreation = DateTime.UtcNow,
            Path = null
        };

        _db.Mockups.Add(mockup);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { prospectId }, mockup);
    }
}

public class MockupDto
{
    public string? UrlPreview { get; set; }
    public string? Commentaire { get; set; }
}

public class NetlifyDeployResponse
{
    [JsonPropertyName("deploy_url")]
    public string? DeployUrl { get; set; }

    [JsonPropertyName("deploy_ssl_url")]
    public string? DeploySslUrl { get; set; }
}
