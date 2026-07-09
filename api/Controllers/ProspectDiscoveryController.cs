using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremierClic.Api.Data;
using PremierClic.Api.Models;

namespace PremierClic.Api.Controllers;

[ApiController]
[Route("api/prospects/discover")]
public class ProspectDiscoveryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ProspectDiscoveryController(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient("overpass");
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> Discover([FromBody] ProspectDiscoveryRequest request)
    {
        var latitude = request.Latitude ?? 42.688; // Perpignan centre
        var longitude = request.Longitude ?? 2.894;
        var radius = request.RadiusMeters > 0 ? request.RadiusMeters : 5000;
        var maxResults = request.MaxResults > 0 ? Math.Min(request.MaxResults, 200) : 100;

        var overpassQuery = BuildOverpassQuery(latitude, longitude, radius, request.Categories ?? new[] { "shop", "amenity" });
        var content = new StringContent(overpassQuery, Encoding.UTF8, "application/x-www-form-urlencoded");

        var overpassUrl = _configuration.GetValue<string>("OVERPASS_BASE_URL") ?? "https://overpass-api.de/api/interpreter";
        var response = await _httpClient.PostAsync(overpassUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, "Erreur Overpass API");
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        var raw = await JsonSerializer.DeserializeAsync<OverpassResponse>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (raw?.Elements == null)
        {
            return Ok(new { imported = 0, prospects = Array.Empty<object>() });
        }

        var created = new List<Prospect>();
        foreach (var element in raw.Elements.Take(maxResults))
        {
            var tags = element.Tags ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (tags.ContainsKey("website") || tags.ContainsKey("contact:website") || tags.ContainsKey("url"))
            {
                continue;
            }

            var name = tags.GetValueOrDefault("name")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var city = tags.GetValueOrDefault("addr:city")?.Trim() ?? tags.GetValueOrDefault("addr:place")?.Trim();
            var address = BuildAddress(tags);
            var category = tags.GetValueOrDefault("shop")?.Trim() ?? tags.GetValueOrDefault("amenity")?.Trim();
            var email = tags.GetValueOrDefault("email")?.Trim();
            var phone = tags.GetValueOrDefault("phone")?.Trim() ?? tags.GetValueOrDefault("contact:phone")?.Trim();

            var existing = await _db.Prospects.FirstOrDefaultAsync(p => p.Nom == name && p.Ville == city);
            if (existing != null)
            {
                continue;
            }

            var prospect = new Prospect
            {
                Id = Guid.NewGuid(),
                Nom = name,
                Categorie = category,
                Adresse = address,
                Ville = city,
                CodePostal = tags.GetValueOrDefault("addr:postcode")?.Trim(),
                Telephone = phone,
                Email = email,
                SourceDonnees = "Overpass",
                ADejaUnSiteWeb = false,
                Statut = ProspectStatus.ANouveauFait,
                Notes = "Import automatique depuis Overpass API",
                DateCreation = DateTime.UtcNow,
                DateDerniereMaj = DateTime.UtcNow
            };

            _db.Prospects.Add(prospect);
            created.Add(prospect);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            imported = created.Count,
            prospects = created.Select(p => new { p.Id, p.Nom, p.Ville, p.Categorie, p.Statut })
        });
    }

    [HttpPost("google")]
    public async Task<IActionResult> DiscoverGoogle([FromBody] ProspectDiscoveryRequest request)
    {
        var apiKey = _configuration["GOOGLE_PLACES_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest("Google Places API key is not configured.");

        var maxResults = request.MaxResults > 0 ? Math.Min(request.MaxResults, 50) : 20;
        var type = request.Categories?.FirstOrDefault() ?? "store";

        string nearbyUrl;
        if (!string.IsNullOrWhiteSpace(request.Ville))
        {
            var query = Uri.EscapeDataString($"commerce {request.Ville}");
            nearbyUrl = $"https://maps.googleapis.com/maps/api/place/textsearch/json?key={apiKey}&query={query}&type={type}";
        }
        else
        {
            var latitude = request.Latitude ?? 42.688;
            var longitude = request.Longitude ?? 2.894;
            var radius = request.RadiusMeters > 0 ? Math.Min(request.RadiusMeters, 50000) : 5000;
            nearbyUrl = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json?key={apiKey}&location={latitude},{longitude}&radius={radius}&keyword=commerce&type={type}";
        }

        var response = await _httpClient.GetAsync(nearbyUrl);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Erreur Google Places API");

        var raw = await response.Content.ReadFromJsonAsync<GoogleNearbyResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (raw == null || (raw.Status != "OK" && raw.Status != "ZERO_RESULTS"))
        {
            return StatusCode(502, new
            {
                imported = 0,
                prospects = Array.Empty<object>(),
                googleStatus = raw?.Status,
                googleError = raw?.ErrorMessage
            });
        }

        if (raw.Results == null)
            return Ok(new { imported = 0, prospects = Array.Empty<object>() });

        var created = new List<Prospect>();
        foreach (var item in raw.Results.Take(maxResults))
        {
            if (string.IsNullOrWhiteSpace(item.PlaceId))
                continue;

            var detailsUrl = $"https://maps.googleapis.com/maps/api/place/details/json?key={apiKey}&place_id={item.PlaceId}&fields=name,formatted_address,website,formatted_phone_number,types";
            var detailsResponse = await _httpClient.GetAsync(detailsUrl);
            if (!detailsResponse.IsSuccessStatusCode)
                continue;

            var details = await detailsResponse.Content.ReadFromJsonAsync<GooglePlaceDetailsResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (details?.Status != "OK" || details.Result == null)
                continue;

            if (!string.IsNullOrWhiteSpace(details.Result.Website))
                continue;

            var name = details.Result.Name?.Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            var address = details.Result.FormattedAddress?.Trim();
            var category = details.Result.Types?.FirstOrDefault();
            var phone = details.Result.FormattedPhoneNumber;

            var existing = await _db.Prospects.FirstOrDefaultAsync(p => p.Nom == name && p.Adresse == address);
            if (existing != null)
                continue;

            var prospect = new Prospect
            {
                Id = Guid.NewGuid(),
                Nom = name,
                Categorie = category,
                Adresse = address,
                Ville = ExtractCity(address),
                CodePostal = ExtractPostalCode(address),
                Telephone = phone,
                Email = null,
                SourceDonnees = "Google Places",
                GooglePlaceId = item.PlaceId,
                ADejaUnSiteWeb = false,
                Statut = ProspectStatus.ANouveauFait,
                Notes = "Import automatique depuis Google Places API",
                DateCreation = DateTime.UtcNow,
                DateDerniereMaj = DateTime.UtcNow
            };

            _db.Prospects.Add(prospect);
            created.Add(prospect);
        }

        await _db.SaveChangesAsync();
        return Ok(new
        {
            imported = created.Count,
            prospects = created.Select(p => new { p.Id, p.Nom, p.Ville, p.Categorie, p.Statut })
        });
    }

    [HttpPost("google/refresh/{id}")]
    public async Task<IActionResult> RefreshGoogle(Guid id)
    {
        var apiKey = _configuration["GOOGLE_PLACES_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest("Google Places API key is not configured.");

        var prospect = await _db.Prospects.FindAsync(id);
        if (prospect == null)
            return NotFound();

        var (changed, found) = await RefreshFromGoogleAsync(prospect, apiKey);
        if (!found)
            return Ok(new { updated = false, found = false });

        if (changed)
        {
            prospect.DateDerniereMaj = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(new { updated = changed, found = true, prospect });
    }

    [HttpGet("google/mockup-prompt/{id}")]
    public async Task<IActionResult> GetMockupPrompt(Guid id)
    {
        var apiKey = _configuration["GOOGLE_PLACES_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest("Google Places API key is not configured.");

        var prospect = await _db.Prospects.FindAsync(id);
        if (prospect == null)
            return NotFound();

        var placeId = await ResolvePlaceIdAsync(prospect, apiKey);
        if (string.IsNullOrWhiteSpace(placeId))
            return Ok(new { found = false, prompt = (string?)null });

        var detailsUrl = $"https://maps.googleapis.com/maps/api/place/details/json?key={apiKey}&place_id={placeId}&fields=name,formatted_address,formatted_phone_number,opening_hours,rating,user_ratings_total,reviews,types&language=fr";
        var detailsResponse = await _httpClient.GetAsync(detailsUrl);
        if (!detailsResponse.IsSuccessStatusCode)
            return Ok(new { found = false, prompt = (string?)null });

        var details = await detailsResponse.Content.ReadFromJsonAsync<GooglePlaceDetailsResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (details?.Status != "OK" || details.Result == null)
            return Ok(new { found = false, prompt = (string?)null });

        if (string.IsNullOrWhiteSpace(prospect.GooglePlaceId))
        {
            prospect.GooglePlaceId = placeId;
            prospect.DateDerniereMaj = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var enrichment = await GetPlaceEnrichmentAsync(placeId, apiKey);
        var businessName = details.Result.Name ?? prospect.Nom;
        var officialPhotos = enrichment?.Photos?
            .Where(p => IsOfficialPhoto(p, businessName))
            .ToList() ?? new List<GooglePhotoRef>();
        var photoUrls = officialPhotos.Count > 0
            ? await GetPhotoUrlsAsync(officialPhotos, apiKey, max: 6)
            : new List<string>();

        var customPhotoUrls = await _db.ProspectPhotoLinks
            .Where(p => p.ProspectId == id)
            .OrderBy(p => p.DateAjout)
            .Select(p => p.Url)
            .ToListAsync();

        var prompt = BuildMockupPrompt(prospect, details.Result, enrichment, photoUrls, customPhotoUrls);
        return Ok(new { found = true, prompt });
    }

    [HttpGet("google/email-prompt/{id}")]
    public async Task<IActionResult> GetEmailPrompt(Guid id, [FromQuery] string? canal)
    {
        var apiKey = _configuration["GOOGLE_PLACES_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest("Google Places API key is not configured.");

        var prospect = await _db.Prospects.FindAsync(id);
        if (prospect == null)
            return NotFound();

        string? businessName = null;
        double? rating = null;
        int? ratingCount = null;
        var reviews = new List<GoogleReview>();

        var placeId = await ResolvePlaceIdAsync(prospect, apiKey);
        if (!string.IsNullOrWhiteSpace(placeId))
        {
            var detailsUrl = $"https://maps.googleapis.com/maps/api/place/details/json?key={apiKey}&place_id={placeId}&fields=name,rating,user_ratings_total,reviews&language=fr";
            var detailsResponse = await _httpClient.GetAsync(detailsUrl);
            if (detailsResponse.IsSuccessStatusCode)
            {
                var details = await detailsResponse.Content.ReadFromJsonAsync<GooglePlaceDetailsResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (details?.Status == "OK" && details.Result != null)
                {
                    businessName = details.Result.Name;
                    rating = details.Result.Rating;
                    ratingCount = details.Result.UserRatingsTotal;
                    reviews = SelectRepresentativeReviews(details.Result.Reviews);
                }
            }
        }

        var mockupUrl = await _db.Mockups
            .Where(m => m.ProspectId == id && m.UrlPreview != null)
            .OrderByDescending(m => m.DateCreation)
            .Select(m => m.UrlPreview)
            .FirstOrDefaultAsync();

        var companyWebsiteUrl = _configuration["COMPANY_WEBSITE_URL"];
        var pricingPageUrl = _configuration["PRICING_PAGE_URL"];

        var prompt = BuildEmailPrompt(prospect, businessName, rating, ratingCount, reviews, mockupUrl, canal, companyWebsiteUrl, pricingPageUrl);
        return Ok(new { prompt, hasMockupUrl = mockupUrl != null });
    }

    private static string BuildEmailPrompt(Prospect prospect, string? businessName, double? rating, int? ratingCount, List<GoogleReview> reviews, string? mockupUrl, string? canal, string? companyWebsiteUrl, string? pricingPageUrl)
    {
        var isEmail = string.Equals(canal, "Email", StringComparison.OrdinalIgnoreCase);
        var canalLabel = string.IsNullOrWhiteSpace(canal) ? "message privé (Instagram/WhatsApp)" : canal;

        var sb = new StringBuilder();
        if (isEmail)
            sb.AppendLine("Rédige un email de prospection commerciale pour proposer à ce commerce local une maquette de site vitrine déjà réalisée gratuitement, en vue de le convaincre de passer à un vrai site internet.");
        else
            sb.AppendLine($"Rédige un message de prospection commerciale, à envoyer via {canalLabel}, pour proposer à ce commerce local une maquette de site vitrine déjà réalisée gratuitement, en vue de le convaincre de passer à un vrai site internet.");
        sb.AppendLine();
        sb.AppendLine($"**Commerce** : {businessName ?? prospect.Nom}");
        sb.AppendLine($"**Catégorie / activité** : {prospect.Categorie ?? "commerce local"}");
        sb.AppendLine($"**Ville** : {prospect.Ville ?? "non renseignée"}");
        if (rating.HasValue)
            sb.AppendLine($"**Note Google** : {rating}/5 ({ratingCount ?? 0} avis)");
        sb.AppendLine();

        if (reviews.Count > 0)
        {
            sb.AppendLine("**Avis clients représentatifs (pour le ton, à ne pas citer littéralement) :**");
            foreach (var r in reviews)
                sb.AppendLine($"- \"{TruncateReviewText(r.Text!)}\"");
            sb.AppendLine();
        }

        var messageWord = isEmail ? "l'email" : "le message";
        if (!string.IsNullOrWhiteSpace(mockupUrl))
        {
            sb.AppendLine($"**Maquette déjà réalisée, à inclure dans {messageWord} sous forme de lien cliquable** : {mockupUrl}");
        }
        else
        {
            sb.AppendLine($"**Aucune maquette déployée pour le moment** : rédige {messageWord} sans lien vers une maquette, en proposant plutôt d'en préparer une gratuitement si le commerçant est intéressé.");
        }

        if (!string.IsNullOrWhiteSpace(companyWebsiteUrl))
            sb.AppendLine($"**Notre site (preuve sociale, à glisser en signature ou en fin de message)** : {companyWebsiteUrl}");

        if (!string.IsNullOrWhiteSpace(pricingPageUrl))
            sb.AppendLine($"**Grille tarifaire (à mentionner brièvement, sans détailler les prix dans {messageWord} lui-même)** : {pricingPageUrl}");

        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Consignes :");
        if (isEmail)
        {
            sb.AppendLine("- Ton professionnel mais chaleureux, direct, pas de jargon marketing ni de formules impersonnelles (\"Cher Monsieur, Chère Madame\").");
            sb.AppendLine("- 100 à 150 mots maximum.");
            sb.AppendLine("- Explique en une phrase que le commerce n'a pas encore de site, mentionne la maquette déjà prête à regarder (avec le lien si disponible), et propose un échange rapide si ça l'intéresse.");
            if (!string.IsNullOrWhiteSpace(companyWebsiteUrl))
                sb.AppendLine("- Glisse le lien de notre site en signature, comme preuve que c'est une vraie entreprise, sans trop insister dessus.");
            sb.AppendLine("- Termine par une signature simple, sans nom d'entreprise inventé.");
            sb.Append("- Réponds uniquement avec deux blocs clairement séparés : \"Objet : ...\" puis \"Corps : ...\" (le corps peut contenir de simples sauts de ligne, pas besoin de HTML).");
        }
        else
        {
            sb.AppendLine($"- Ton chaleureux, direct et décontracté, adapté à un message privé sur {canalLabel} (pas un email formel) : pas de \"Bonjour Madame/Monsieur\", plutôt une accroche naturelle.");
            sb.AppendLine("- 50 à 80 mots maximum, format court adapté à un message privé.");
            sb.AppendLine("- Explique en une phrase que le commerce n'a pas encore de site, mentionne la maquette déjà prête à regarder (avec le lien si disponible), et propose un échange rapide si ça l'intéresse.");
            if (!string.IsNullOrWhiteSpace(companyWebsiteUrl))
                sb.AppendLine("- Glisse le lien de notre site en signature, comme preuve que c'est une vraie entreprise, sans trop insister dessus.");
            sb.AppendLine("- Termine par une signature simple, sans nom d'entreprise inventé.");
            sb.Append("- Réponds uniquement avec le texte du message, prêt à copier-coller directement, sans \"Objet :\" ni formule d'email.");
        }

        return sb.ToString();
    }

    private async Task<GooglePlaceEnrichment?> GetPlaceEnrichmentAsync(string placeId, string apiKey)
    {
        var url = $"https://places.googleapis.com/v1/places/{placeId}?fields=accessibilityOptions,paymentOptions,parkingOptions,photos";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Goog-Api-Key", apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<GooglePlaceEnrichment>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static bool IsOfficialPhoto(GooglePhotoRef photo, string businessName)
    {
        var author = photo.AuthorAttributions?.FirstOrDefault()?.DisplayName;
        if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(businessName))
            return false;

        return string.Equals(author.Trim(), businessName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> GetPhotoUrlsAsync(List<GooglePhotoRef> photos, string apiKey, int max = 3)
    {
        var urls = new List<string>();
        foreach (var photo in photos.Take(max))
        {
            if (string.IsNullOrWhiteSpace(photo.Name))
                continue;

            var mediaUrl = $"https://places.googleapis.com/v1/{photo.Name}/media?maxWidthPx=1024&skipHttpRedirect=true";
            var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
            request.Headers.Add("X-Goog-Api-Key", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                continue;

            var media = await response.Content.ReadFromJsonAsync<GooglePhotoMedia>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (!string.IsNullOrWhiteSpace(media?.PhotoUri))
                urls.Add(media.PhotoUri);
        }
        return urls;
    }

    private static List<GoogleReview> SelectRepresentativeReviews(List<GoogleReview>? reviews, int max = 3)
    {
        var candidateReviews = (reviews ?? new List<GoogleReview>())
            .Where(r => !string.IsNullOrWhiteSpace(r.Text))
            .ToList();

        var selected = candidateReviews
            .Where(r => (r.Rating ?? 0) >= 4)
            .OrderByDescending(r => r.Rating ?? 0)
            .ThenBy(r => r.Text!.Length)
            .Take(max)
            .ToList();

        if (selected.Count == 0)
        {
            selected = candidateReviews
                .OrderByDescending(r => r.Rating ?? 0)
                .ThenBy(r => r.Text!.Length)
                .Take(max)
                .ToList();
        }

        return selected;
    }

    private static string BuildMockupPrompt(Prospect prospect, GooglePlaceDetailsResult details, GooglePlaceEnrichment? enrichment, List<string> photoUrls, List<string> customPhotoUrls)
    {
        var reviews = SelectRepresentativeReviews(details.Reviews);

        var sb = new StringBuilder();
        sb.AppendLine("Crée une maquette de site vitrine pour ce commerce local, à partir des informations de sa fiche Google Business.");
        sb.AppendLine();
        sb.AppendLine($"**Nom** : {prospect.Nom}");
        sb.AppendLine($"**Catégorie / activité** : {prospect.Categorie ?? "commerce local"}");
        sb.AppendLine($"**Adresse** : {details.FormattedAddress ?? prospect.Adresse}");
        sb.AppendLine($"**Téléphone** : {details.FormattedPhoneNumber ?? prospect.Telephone ?? "non renseigné"}");
        if (details.OpeningHours?.WeekdayText is { Count: > 0 } weekdayText)
            sb.AppendLine($"**Horaires** : {string.Join(" / ", weekdayText)}");
        if (details.Rating.HasValue)
            sb.AppendLine($"**Note Google** : {details.Rating}/5 ({details.UserRatingsTotal ?? 0} avis)");
        sb.AppendLine();

        if (reviews.Count > 0)
        {
            sb.AppendLine("**Avis clients représentatifs (pour le ton) :**");
            foreach (var r in reviews)
                sb.AppendLine($"- \"{TruncateReviewText(r.Text!)}\"");
            sb.AppendLine();
        }

        var attributes = BuildAttributeLabels(enrichment);
        if (attributes.Count > 0)
        {
            sb.AppendLine($"**Informations pratiques** : {string.Join(", ", attributes)}");
            sb.AppendLine();
        }

        if (photoUrls.Count > 0)
        {
            sb.AppendLine("**Photos officielles de l'établissement (uploadées par le commerce, pas des photos d'avis clients) — à télécharger et utiliser dans la maquette :**");
            foreach (var url in photoUrls)
                sb.AppendLine($"- {url}");
            sb.AppendLine();
        }

        if (customPhotoUrls.Count > 0)
        {
            sb.AppendLine("**Photos supplémentaires sélectionnées manuellement — à télécharger et utiliser dans la maquette :**");
            foreach (var url in customPhotoUrls)
                sb.AppendLine($"- {url}");
            sb.AppendLine();
        }

        sb.AppendLine("**Autres images :** je vais également te fournir en copier-collé directement dans la conversation d'autres photos de l'établissement. Utilise-les dans la maquette (galerie, sections pertinentes) au même titre que les photos ci-dessus.");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Objectif : une page d'accueil de site vitrine moderne et professionnelle qui donne envie de pousser la porte. Le ton doit correspondre à l'activité (chaleureux/artisanal, rassurant/qualité, etc. — adapte selon le commerce ci-dessus).");
        sb.AppendLine();
        sb.AppendLine("Structure attendue :");
        var steps = new List<string>
        {
            "Hero : nom + accroche courte + suggestion visuelle",
            "Présentation en 2-3 phrases (ton adapté au secteur)"
        };
        steps.Add("Galerie avec les photos fournies (URLs ci-dessus et/ou images collées dans la conversation)");
        steps.Add("Horaires, adresse, bouton \"Itinéraire\"");
        if (attributes.Count > 0)
            steps.Add("Informations pratiques (accessibilité, moyens de paiement, parking)");
        if (reviews.Count > 0)
            steps.Add("Témoignages clients");
        steps.Add("Call-to-action (appeler / venir)");

        for (var i = 0; i < steps.Count; i++)
            sb.AppendLine($"{i + 1}. {steps[i]}");
        sb.AppendLine();
        sb.AppendLine("Palette de couleurs cohérente avec le secteur. Design sobre, sans jargon marketing.");
        sb.AppendLine();
        sb.AppendLine("Important : conçois en mobile-first, la majorité des visiteurs de ce type de commerce consultent le site depuis leur téléphone. Boutons et zones cliquables assez grands pour le doigt, texte lisible sans zoomer, sections empilées verticalement, temps de chargement rapide (images optimisées). Vérifie ensuite que le rendu reste cohérent sur tablette et desktop.");
        sb.AppendLine();
        sb.AppendLine("Attention scroll mobile : n'utilise pas de `height: 100vh` ni d'`overflow: hidden` fixe sur le body, le html ou un conteneur englobant la page — ça empêche de scroller jusqu'en bas sur mobile et coupe le contenu (notamment le call-to-action final). La page doit pouvoir scroller normalement sur toute sa hauteur, sans zone de contenu tronquée.");
        sb.AppendLine();
        sb.Append("Important : ajoute un bandeau discret juste en dessous du header (pas superposé dessus) indiquant qu'il s'agit d'un aperçu, par exemple « Aperçu de maquette — site pas encore en ligne ». Ce bandeau doit rester lisible mais sobre (ne pas dominer visuellement la page), pour éviter que le commerçant ne croie que son site est déjà en ligne.");

        return sb.ToString();
    }

    private static List<string> BuildAttributeLabels(GooglePlaceEnrichment? enrichment)
    {
        var labels = new List<string>();
        if (enrichment == null)
            return labels;

        var acc = enrichment.AccessibilityOptions;
        if (acc?.WheelchairAccessibleEntrance == true) labels.Add("entrée accessible en fauteuil roulant");
        if (acc?.WheelchairAccessibleParking == true) labels.Add("parking accessible en fauteuil roulant");
        if (acc?.WheelchairAccessibleRestroom == true) labels.Add("toilettes accessibles en fauteuil roulant");
        if (acc?.WheelchairAccessibleSeating == true) labels.Add("places assises accessibles en fauteuil roulant");

        var pay = enrichment.PaymentOptions;
        if (pay?.AcceptsCreditCards == true) labels.Add("cartes de crédit acceptées");
        if (pay?.AcceptsDebitCards == true) labels.Add("cartes de débit acceptées");
        if (pay?.AcceptsNfc == true) labels.Add("paiement sans contact (NFC)");
        if (pay?.AcceptsCashOnly == true) labels.Add("espèces uniquement");

        var park = enrichment.ParkingOptions;
        if (park?.FreeParkingLot == true) labels.Add("parking gratuit");
        if (park?.PaidParkingLot == true) labels.Add("parking payant");
        if (park?.FreeStreetParking == true) labels.Add("stationnement gratuit dans la rue");
        if (park?.PaidStreetParking == true) labels.Add("stationnement payant dans la rue");
        if (park?.ValetParking == true) labels.Add("voiturier");

        return labels;
    }

    private static string TruncateReviewText(string text)
    {
        const int maxLength = 280;
        var normalized = text.Replace("\r\n", " ").Replace("\n", " ").Trim();
        if (normalized.Length <= maxLength)
            return normalized;

        var cut = normalized.Substring(0, maxLength);
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0)
            cut = cut.Substring(0, lastSpace);

        return cut.TrimEnd('.', ',', ' ') + "…";
    }

    [HttpPost("google/refresh-existing")]
    public async Task<IActionResult> RefreshExistingGoogle([FromQuery] int maxResults = 25)
    {
        var apiKey = _configuration["GOOGLE_PLACES_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest("Google Places API key is not configured.");

        var candidates = await _db.Prospects
            .Where(p => p.SourceDonnees == "Google Places" && (p.GooglePlaceId == null || p.Telephone == null))
            .Take(Math.Clamp(maxResults, 1, 50))
            .ToListAsync();

        var updated = new List<Prospect>();
        var notFoundCount = 0;
        foreach (var prospect in candidates)
        {
            var (changed, found) = await RefreshFromGoogleAsync(prospect, apiKey);
            if (!found)
            {
                notFoundCount++;
                continue;
            }
            if (changed)
            {
                prospect.DateDerniereMaj = DateTime.UtcNow;
                updated.Add(prospect);
            }
        }

        if (updated.Count > 0)
            await _db.SaveChangesAsync();

        return Ok(new
        {
            checkedCount = candidates.Count,
            updated = updated.Count,
            notFound = notFoundCount,
            prospects = updated.Select(p => new { p.Id, p.Nom, p.Ville, p.Telephone, p.GooglePlaceId })
        });
    }

    private async Task<string?> ResolvePlaceIdAsync(Prospect prospect, string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(prospect.GooglePlaceId))
            return prospect.GooglePlaceId;

        var query = Uri.EscapeDataString($"{prospect.Nom} {prospect.Adresse ?? prospect.Ville}".Trim());
        var findUrl = $"https://maps.googleapis.com/maps/api/place/findplacefromtext/json?input={query}&inputtype=textquery&fields=place_id&key={apiKey}";
        var findResponse = await _httpClient.GetAsync(findUrl);
        if (!findResponse.IsSuccessStatusCode)
            return null;

        var find = await findResponse.Content.ReadFromJsonAsync<GoogleFindPlaceResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return find?.Candidates?.FirstOrDefault()?.PlaceId;
    }

    private async Task<(bool changed, bool found)> RefreshFromGoogleAsync(Prospect prospect, string apiKey)
    {
        var placeId = await ResolvePlaceIdAsync(prospect, apiKey);
        if (string.IsNullOrWhiteSpace(placeId))
            return (false, false);

        var detailsUrl = $"https://maps.googleapis.com/maps/api/place/details/json?key={apiKey}&place_id={placeId}&fields=name,formatted_address,website,formatted_phone_number,types";
        var detailsResponse = await _httpClient.GetAsync(detailsUrl);
        if (!detailsResponse.IsSuccessStatusCode)
            return (false, false);

        var details = await detailsResponse.Content.ReadFromJsonAsync<GooglePlaceDetailsResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (details?.Status != "OK" || details.Result == null)
            return (false, false);

        var changed = false;
        if (string.IsNullOrWhiteSpace(prospect.GooglePlaceId))
        {
            prospect.GooglePlaceId = placeId;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(prospect.Telephone) && !string.IsNullOrWhiteSpace(details.Result.FormattedPhoneNumber))
        {
            prospect.Telephone = details.Result.FormattedPhoneNumber;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(prospect.Adresse) && !string.IsNullOrWhiteSpace(details.Result.FormattedAddress))
        {
            prospect.Adresse = details.Result.FormattedAddress;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(details.Result.Website) && !prospect.ADejaUnSiteWeb)
        {
            prospect.ADejaUnSiteWeb = true;
            changed = true;
        }

        return (changed, true);
    }

    private static string? ExtractPostalCode(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var parts = address.Split(',').Select(p => p.Trim()).ToArray();
        return parts.Reverse().FirstOrDefault(part => part.Any(char.IsDigit));
    }

    private static string? ExtractCity(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var parts = address.Split(',').Select(p => p.Trim()).ToArray();
        return parts.Length > 1 ? parts[^2] : parts.FirstOrDefault();
    }

    private static string BuildOverpassQuery(double lat, double lon, int radius, string[] categories)
    {
        var categoryFilters = string.Join(";\n", categories.Select(cat => $"  node[{cat}](around:{radius},{lat},{lon});\n  way[{cat}](around:{radius},{lat},{lon});\n  relation[{cat}](around:{radius},{lat},{lon});"));
        return "[out:json][timeout:25];\n(\n" + categoryFilters + ")\nout center tags;";
    }

    private static string BuildAddress(Dictionary<string, string> tags)
    {
        var parts = new List<string>();
        if (tags.TryGetValue("addr:housenumber", out var house) && !string.IsNullOrWhiteSpace(house)) parts.Add(house.Trim());
        if (tags.TryGetValue("addr:street", out var street) && !string.IsNullOrWhiteSpace(street)) parts.Add(street.Trim());
        if (tags.TryGetValue("addr:postcode", out var postcode) && !string.IsNullOrWhiteSpace(postcode)) parts.Add(postcode.Trim());
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}

public class ProspectDiscoveryRequest
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public int MaxResults { get; set; }
    public string[]? Categories { get; set; }
    public string? Ville { get; set; }
}

public class OverpassResponse
{
    public List<OverpassElement>? Elements { get; set; }
}

public class OverpassElement
{
    public long Id { get; set; }
    public string? Type { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class GoogleNearbyResponse
{
    public string? Status { get; set; }
    public List<GoogleNearbyPlace>? Results { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

public class GoogleNearbyPlace
{
    [System.Text.Json.Serialization.JsonPropertyName("place_id")]
    public string? PlaceId { get; set; }
    public string? Name { get; set; }
    public string? Vicinity { get; set; }
}

public class GoogleFindPlaceResponse
{
    public string? Status { get; set; }
    public List<GoogleFindPlaceCandidate>? Candidates { get; set; }
}

public class GoogleFindPlaceCandidate
{
    [System.Text.Json.Serialization.JsonPropertyName("place_id")]
    public string? PlaceId { get; set; }
}

public class GooglePlaceDetailsResponse
{
    public string? Status { get; set; }
    public GooglePlaceDetailsResult? Result { get; set; }
}

public class GooglePlaceDetailsResult
{
    public string? Name { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("formatted_address")]
    public string? FormattedAddress { get; set; }
    public string? Website { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("formatted_phone_number")]
    public string? FormattedPhoneNumber { get; set; }
    public List<string>? Types { get; set; }
    public double? Rating { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("user_ratings_total")]
    public int? UserRatingsTotal { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("opening_hours")]
    public GoogleOpeningHours? OpeningHours { get; set; }
    public List<GoogleReview>? Reviews { get; set; }
}

public class GoogleOpeningHours
{
    [System.Text.Json.Serialization.JsonPropertyName("weekday_text")]
    public List<string>? WeekdayText { get; set; }
}

public class GoogleReview
{
    [System.Text.Json.Serialization.JsonPropertyName("author_name")]
    public string? AuthorName { get; set; }
    public double? Rating { get; set; }
    public string? Text { get; set; }
}

// New Places API (places.googleapis.com/v1) — camelCase JSON, distinct from the legacy DTOs above.
public class GooglePlaceEnrichment
{
    public GoogleAccessibilityOptions? AccessibilityOptions { get; set; }
    public GooglePaymentOptions? PaymentOptions { get; set; }
    public GoogleParkingOptions? ParkingOptions { get; set; }
    public List<GooglePhotoRef>? Photos { get; set; }
}

public class GoogleAccessibilityOptions
{
    public bool? WheelchairAccessibleParking { get; set; }
    public bool? WheelchairAccessibleEntrance { get; set; }
    public bool? WheelchairAccessibleRestroom { get; set; }
    public bool? WheelchairAccessibleSeating { get; set; }
}

public class GooglePaymentOptions
{
    public bool? AcceptsCreditCards { get; set; }
    public bool? AcceptsDebitCards { get; set; }
    public bool? AcceptsCashOnly { get; set; }
    public bool? AcceptsNfc { get; set; }
}

public class GoogleParkingOptions
{
    public bool? FreeParkingLot { get; set; }
    public bool? PaidParkingLot { get; set; }
    public bool? FreeStreetParking { get; set; }
    public bool? PaidStreetParking { get; set; }
    public bool? ValetParking { get; set; }
}

public class GooglePhotoRef
{
    public string? Name { get; set; }
    public List<GoogleAuthorAttribution>? AuthorAttributions { get; set; }
}

public class GoogleAuthorAttribution
{
    public string? DisplayName { get; set; }
}

public class GooglePhotoMedia
{
    public string? PhotoUri { get; set; }
}
