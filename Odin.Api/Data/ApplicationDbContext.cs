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
        public DbSet<TimeEra> TimeEras { get; set; }
        public DbSet<SubEra> SubEras { get; set; }
        public DbSet<TimeEraSubEra> TimeEraSubEras { get; set; }
        public DbSet<QpadmResult> QpadmResults { get; set; }
        public DbSet<VahaduoResult> VahaduoResults { get; set; }
        public DbSet<ResearchLink> ResearchLinks { get; set; }
        public DbSet<QpadmResultResearchLink> QpadmResultResearchLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
