using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<RawGeneticFile> RawGeneticFiles { get; set; }
        public DbSet<GeneticInspection> GeneticInspections { get; set; }
        public DbSet<GeneticInspectionRegion> GeneticInspectionRegions { get; set; }
        public DbSet<Ethnicity> Ethnicities { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<Era> Eras { get; set; }
        public DbSet<Population> Populations { get; set; }
        public DbSet<MusicTrack> MusicTracks { get; set; }
        public DbSet<QpadmResult> QpadmResults { get; set; }
        public DbSet<QpadmResultEraGroup> QpadmResultEraGroups { get; set; }
        public DbSet<ResearchLink> ResearchLinks { get; set; }
        public DbSet<QpadmResultResearchLink> QpadmResultResearchLinks { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<CatalogProduct> CatalogProducts { get; set; }
        public DbSet<ProductAddon> ProductAddons { get; set; }
        public DbSet<CatalogProductAddon> CatalogProductAddons { get; set; }
        public DbSet<OrderLineAddon> OrderLineAddons { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Log> Logs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
