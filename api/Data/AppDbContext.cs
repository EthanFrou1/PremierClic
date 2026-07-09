using Microsoft.EntityFrameworkCore;
using PremierClic.Api.Models;

namespace PremierClic.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Prospect> Prospects => Set<Prospect>();
    public DbSet<Mockup> Mockups => Set<Mockup>();
    public DbSet<EmailEnvoye> EmailEnvoyes => Set<EmailEnvoye>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProspectPhotoLink> ProspectPhotoLinks => Set<ProspectPhotoLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Prospect>().HasKey(p => p.Id);
        modelBuilder.Entity<Mockup>().HasKey(m => m.Id);
        modelBuilder.Entity<EmailEnvoye>().HasKey(e => e.Id);
        modelBuilder.Entity<EmailTemplate>().HasKey(t => t.Id);
        modelBuilder.Entity<User>().HasKey(u => u.Id);
        modelBuilder.Entity<ProspectPhotoLink>().HasKey(p => p.Id);
    }
}
