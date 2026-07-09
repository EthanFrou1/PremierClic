using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
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

        await using var stream = file.OpenReadStream();
        var (success, statusCode, result) = await ZipAndDeployToNetlifyAsync(stream, apiToken, siteId);
        if (!success) return StatusCode(statusCode, $"Échec du déploiement Netlify : {result}");

        var mockup = new Mockup
        {
            Id = Guid.NewGuid(),
            ProspectId = prospectId,
            UrlPreview = result,
            Commentaire = commentaire,
            DateCreation = DateTime.UtcNow,
            Path = null
        };

        _db.Mockups.Add(mockup);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { prospectId }, mockup);
    }

    [HttpPost("{mockupId}/deploy")]
    public async Task<IActionResult> DeployExisting(Guid prospectId, Guid mockupId)
    {
        var mockup = await _db.Mockups.FirstOrDefaultAsync(m => m.Id == mockupId && m.ProspectId == prospectId);
        if (mockup == null) return NotFound();
        if (string.IsNullOrWhiteSpace(mockup.Path)) return BadRequest("Cette maquette n'a pas de fichier uploadé à déployer.");

        var apiToken = _configuration["NETLIFY_API_TOKEN"];
        var siteId = _configuration["NETLIFY_SITE_ID"];
        if (string.IsNullOrWhiteSpace(apiToken) || string.IsNullOrWhiteSpace(siteId))
            return BadRequest("Le déploiement Netlify n'est pas configuré (NETLIFY_API_TOKEN / NETLIFY_SITE_ID).");

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", mockup.Path);
        if (!System.IO.File.Exists(filePath)) return NotFound("Fichier introuvable sur le serveur.");

        await using var fileStream = System.IO.File.OpenRead(filePath);
        var (success, statusCode, result) = await ZipAndDeployToNetlifyAsync(fileStream, apiToken, siteId);
        if (!success) return StatusCode(statusCode, $"Échec du déploiement Netlify : {result}");

        mockup.UrlPreview = result;
        await _db.SaveChangesAsync();
        return Ok(mockup);
    }

    private async Task<(bool success, int statusCode, string? deployUrlOrError)> ZipAndDeployToNetlifyAsync(Stream fileContent, string apiToken, string siteId)
    {
        string html;
        using (var reader = new StreamReader(fileContent, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            html = await reader.ReadToEndAsync();
        }
        html = InjectPreviewBanner(html);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("index.html", CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(html);
            }

            // Netlify's raw-zip deploy method doesn't reliably infer Content-Type from file
            // extension, and serves index.html as text/plain without this. Force it explicitly.
            // Each entry's stream must be closed before the next one is created, or ZipArchive throws.
            var headersEntry = archive.CreateEntry("_headers", CompressionLevel.Optimal);
            using (var headersStream = headersEntry.Open())
            using (var headersWriter = new StreamWriter(headersStream))
            {
                await headersWriter.WriteAsync("/*\n  Content-Type: text/html; charset=UTF-8\n");
            }
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
            return (false, (int)response.StatusCode, errorBody);
        }

        var deploy = await response.Content.ReadFromJsonAsync<NetlifyDeployResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var deployUrl = deploy?.DeploySslUrl ?? deploy?.DeployUrl;
        if (string.IsNullOrWhiteSpace(deployUrl))
            return (false, 502, "Netlify n'a pas renvoyé d'URL de déploiement.");

        return (true, 200, deployUrl);
    }

    // Claude Design exports sometimes unpack their content into <body> via JS after load
    // (the "Bundled Page" mechanism), so a plain static insertion at parse time can get wiped.
    // This retries for a few seconds to find the real <header> once the page has settled, and
    // falls back to the top of <body> if none is found.
    private static string InjectPreviewBanner(string html)
    {
        const string banner = @"
<script>(function(){
  function insertBanner(){
    if (document.getElementById('__pc_preview_banner')) return;
    var b = document.createElement('div');
    b.id = '__pc_preview_banner';
    b.textContent = 'Aperçu de maquette — site pas encore en ligne (démonstration)';
    b.style.cssText = 'background:#111;color:#fff;text-align:center;padding:8px 12px;font:600 13px/1.4 -apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;position:relative;z-index:2147483647;';
    var header = document.querySelector('header') || (document.body && document.body.firstElementChild);
    if (header && header.parentNode) header.parentNode.insertBefore(b, header.nextSibling);
    else if (document.body) document.body.insertBefore(b, document.body.firstChild);
  }
  var tries = 0;
  var t = setInterval(function(){
    tries++;
    if (document.body && (document.querySelector('header') || tries > 20)) { insertBanner(); clearInterval(t); }
  }, 250);
  window.addEventListener('load', insertBanner);
})();</script>
";

        var closingBodyIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return closingBodyIndex >= 0 ? html.Insert(closingBodyIndex, banner) : html + banner;
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
