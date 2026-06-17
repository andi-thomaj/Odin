namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// Marks an entity as owned by a single application (multi-app data isolation). Implemented ONLY by
    /// user-owned tables — shared reference/seed data must NOT implement it, so it stays common to every app.
    /// <see cref="App"/> holds the owning <c>applications.key</c>. <see cref="Data.ApplicationDbContext"/>
    /// auto-stamps it on insert and applies a global query filter (<c>App == current request app</c>) so reads
    /// are scoped without per-query code. Cross-app/admin reads opt out with <c>IgnoreQueryFilters()</c>.
    /// </summary>
    public interface IAppScoped
    {
        string App { get; set; }
    }
}
