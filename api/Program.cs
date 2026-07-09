using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;
using PremierClic.Api.Data;
using PremierClic.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for local dev
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("LocalDev", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:3000", "http://127.0.0.1:5173")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Database
var connection = builder.Configuration.GetValue<string>("ConnectionStrings:DefaultConnection") ?? builder.Configuration.GetValue<string>("DATABASE_URL") ?? "Host=db;Port=5432;Database=premierclic;Username=premierclic;Password=premierclic";
builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(connection));

// Register JwtService
builder.Services.AddSingleton<PremierClic.Api.Services.JwtService>();

var app = builder.Build();

// Ensure DB is created and migrations applied at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Apply any drift fixes for existing databases
    db.Database.ExecuteSqlRaw("ALTER TABLE \"Prospects\" ADD COLUMN IF NOT EXISTS \"TokenDesinscription\" text;");
    db.Database.ExecuteSqlRaw("ALTER TABLE \"EmailEnvoyes\" ADD COLUMN IF NOT EXISTS \"TokenDesinscription\" text;");

    // Seed admin user if none exists and env vars provided
    var adminEmail = builder.Configuration.GetValue<string>("ADMIN_EMAIL");
    var adminPassword = builder.Configuration.GetValue<string>("ADMIN_PASSWORD");
    if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
    {
        if (!db.Users.Any(u => u.Email == adminEmail))
        {
            var hashed = BCrypt.Net.BCrypt.HashPassword(adminPassword);
            db.Users.Add(new User { Email = adminEmail, PasswordHash = hashed, IsAdmin = true, CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("LocalDev");

var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapGet("/", () => Results.Ok("PremierClic API"));
app.MapControllers();

app.Run();
