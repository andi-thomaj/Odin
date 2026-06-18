using Microsoft.EntityFrameworkCore;
using Odin.Api.Authentication;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IAppContext appContext)
        : DbContext(options)
    {
        // Resolved per request by AppResolutionMiddleware; drives the app-scoped query filters + write
        // stamping below. Defaults to AppKeys.Ancestrify outside a request (background jobs) — see IAppContext.
        private readonly IAppContext _appContext = appContext;

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
        public DbSet<QpadmCladeResult> QpadmCladeResults { get; set; }
        public DbSet<QpadmResultEraGroup> QpadmResultEraGroups { get; set; }
        public DbSet<QpadmOrder> QpadmOrders { get; set; }
        public DbSet<G25Order> G25Orders { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<MusicTrackFile> MusicTrackFiles { get; set; }
        public DbSet<G25AdmixturePopulationSample> G25AdmixturePopulationSamples { get; set; }
        public DbSet<G25DistancePopulationSample> G25DistancePopulationSamples { get; set; }
        public DbSet<G25PcaPopulationsSample> G25PcaPopulationsSamples { get; set; }
        public DbSet<QpadmPopulationSample> QpadmPopulationSamples { get; set; }
        public DbSet<QpadmPopulationPanelSample> QpadmPopulationPanelSamples { get; set; }
        public DbSet<ResearchLink> ResearchLinks { get; set; }
        public DbSet<G25SavedCoordinate> G25SavedCoordinates { get; set; }
        public DbSet<G25TargetCoordinate> G25TargetCoordinates { get; set; }
        public DbSet<G25Region> G25Regions { get; set; }
        public DbSet<G25Ethnicity> G25Ethnicities { get; set; }
        public DbSet<G25Continent> G25Continents { get; set; }
        public DbSet<G25DistanceEra> G25DistanceEras { get; set; }
        public DbSet<G25AdmixtureEra> G25AdmixtureEras { get; set; }
        public DbSet<G25GeneticInspection> G25GeneticInspections { get; set; }
        public DbSet<G25GeneticInspectionEthnicity> G25GeneticInspectionEthnicities { get; set; }
        public DbSet<G25GeneticInspectionRegion> G25GeneticInspectionRegions { get; set; }
        public DbSet<G25GeneticInspectionContinent> G25GeneticInspectionContinents { get; set; }
        public DbSet<G25DistanceResult> G25DistanceResults { get; set; }
        public DbSet<G25AdmixtureResult> G25AdmixtureResults { get; set; }
        public DbSet<G25PcaResult> G25PcaResults { get; set; }
        public DbSet<Calculator> Calculators { get; set; }
        public DbSet<AdmixToolsEra> AdmixToolsEras { get; set; }
        public DbSet<Application> Applications { get; set; }

        // Y-haplogroup heatmap reference data (imported from odin-tools-api; shared, NOT app-scoped).
        public DbSet<YHaplogroupSample> YHaplogroupSamples { get; set; }
        public DbSet<YHaplogroupTreeNode> YHaplogroupTreeNodes { get; set; }
        public DbSet<HaplogroupImportRun> HaplogroupImportRuns { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // ── Multi-app data isolation ─────────────────────────────────────────────────────────────────
            // Every IAppScoped entity is filtered to the current request's app, so reads cannot see another
            // app's rows without an explicit IgnoreQueryFilters() (admin/cross-app + background paths). The
            // filter references the injected IAppContext instance, which EF re-evaluates per query. Reference/
            // seed tables are NOT IAppScoped and stay common to every app. Writes are stamped in SaveChanges.
            modelBuilder.Entity<User>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<QpadmOrder>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<G25Order>().HasQueryFilter(e => e.App == _appContext.App);
            // RawGeneticFile folds its existing soft-delete predicate in (one query filter per entity allowed).
            modelBuilder.Entity<RawGeneticFile>().HasQueryFilter(e => !e.IsDeleted && e.App == _appContext.App);
            modelBuilder.Entity<QpadmGeneticInspection>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<G25GeneticInspection>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<Report>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<Notification>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<G25SavedCoordinate>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<G25TargetCoordinate>().HasQueryFilter(e => e.App == _appContext.App);
            // Calculator: admin/global rows (IsAdmin) are visible in every app; user rows are app-scoped.
            modelBuilder.Entity<Calculator>().HasQueryFilter(e => e.IsAdmin || e.App == _appContext.App);
            modelBuilder.Entity<QpadmResult>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<QpadmCladeResult>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<G25DistanceResult>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<G25AdmixtureResult>().HasQueryFilter(e => e.App == _appContext.App);
            modelBuilder.Entity<G25PcaResult>().HasQueryFilter(e => e.App == _appContext.App);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampAppScoped();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            StampAppScoped();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // Stamp the owning app on newly-added IAppScoped rows that don't already carry one. Code that sets App
        // explicitly (e.g. provisioning, or a background job copying the parent inspection's app onto a result)
        // wins, because we only fill when it's empty.
        private void StampAppScoped()
        {
            var app = _appContext.App;
            foreach (var entry in ChangeTracker.Entries<IAppScoped>())
            {
                if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.App))
                    entry.Entity.App = app;
            }
        }
    }
}
