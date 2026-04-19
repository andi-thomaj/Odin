using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<RawGeneticFile> RawGeneticFiles { get; set; }
        public DbSet<QpadmGeneticInspection> QpadmGeneticInspections { get; set; }
        public DbSet<QpadmGeneticInspectionRegion> QpadmGeneticInspectionRegions { get; set; }
        public DbSet<QpadmEthnicity> QpadmEthnicities { get; set; }
        public DbSet<QpadmRegion> QpadmRegions { get; set; }
        public DbSet<QpadmEra> QpadmEras { get; set; }
        public DbSet<QpadmPopulation> QpadmPopulations { get; set; }
        public DbSet<MusicTrack> MusicTracks { get; set; }
        public DbSet<QpadmResult> QpadmResults { get; set; }
        public DbSet<QpadmResultEraGroup> QpadmResultEraGroups { get; set; }
        public DbSet<ResearchLink> ResearchLinks { get; set; }
        public DbSet<QpadmResultResearchLink> QpadmResultResearchLinks { get; set; }
        public DbSet<QpadmOrder> QpadmOrders { get; set; }
        public DbSet<G25Order> G25Orders { get; set; }
        public DbSet<CatalogProduct> CatalogProducts { get; set; }
        public DbSet<ProductAddon> ProductAddons { get; set; }
        public DbSet<CatalogProductAddon> CatalogProductAddons { get; set; }
        public DbSet<OrderLineAddon> OrderLineAddons { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<MusicTrackFile> MusicTrackFiles { get; set; }
        public DbSet<ChangelogVersion> ChangelogVersions { get; set; }
        public DbSet<ChangelogEntry> ChangelogEntries { get; set; }
        public DbSet<G25Ancient> G25Ancients { get; set; }
        public DbSet<G25SavedCoordinate> G25SavedCoordinates { get; set; }
        public DbSet<AdmixtureSavedFile> AdmixtureSavedFiles { get; set; }
        public DbSet<PaddlePayment> PaddlePayments { get; set; }
        public DbSet<G25Region> G25Regions { get; set; }
        public DbSet<G25Ethnicity> G25Ethnicities { get; set; }
        public DbSet<G25Continent> G25Continents { get; set; }
        public DbSet<G25DistanceEra> G25DistanceEras { get; set; }
        public DbSet<G25AdmixtureEra> G25AdmixtureEras { get; set; }
        public DbSet<G25DistanceFile> G25DistanceFiles { get; set; }
        public DbSet<G25AdmixtureFile> G25AdmixtureFiles { get; set; }
        public DbSet<G25PcaFile> G25PcaFiles { get; set; }
        public DbSet<G25GeneticInspection> G25GeneticInspections { get; set; }
        public DbSet<G25GeneticInspectionEthnicity> G25GeneticInspectionEthnicities { get; set; }
        public DbSet<G25GeneticInspectionRegion> G25GeneticInspectionRegions { get; set; }
        public DbSet<G25GeneticInspectionContinent> G25GeneticInspectionContinents { get; set; }
        public DbSet<G25DistanceResult> G25DistanceResults { get; set; }
        public DbSet<G25AdmixtureResult> G25AdmixtureResults { get; set; }
        public DbSet<G25PcaResult> G25PcaResults { get; set; }
        public DbSet<G25PcaResultFile> G25PcaResultFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
