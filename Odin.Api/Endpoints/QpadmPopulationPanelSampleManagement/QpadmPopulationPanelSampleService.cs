using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement.Models;

namespace Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement;

public interface IQpadmPopulationPanelSampleService
{
    Task<IReadOnlyList<GetPanelSampleLinksContract.Response>> GetLinksAsync(string panel,
        CancellationToken cancellationToken = default);

    Task<SetSamplePopulationsContract.Response> SetSamplePopulationsAsync(string identityId,
        SetSamplePopulationsContract.Request request, CancellationToken cancellationToken = default);

    Task<BulkAssignSamplePopulationsContract.Response> BulkAssignAsync(string identityId,
        BulkAssignSamplePopulationsContract.Request request, CancellationToken cancellationToken = default);
}

public class QpadmPopulationPanelSampleService(ApplicationDbContext dbContext) : IQpadmPopulationPanelSampleService
{
    public async Task<IReadOnlyList<GetPanelSampleLinksContract.Response>> GetLinksAsync(string panel,
        CancellationToken cancellationToken = default)
    {
        var normalizedPanel = panel.Trim();

        return await dbContext.QpadmPopulationPanelSamples
            .AsNoTracking()
            .Where(e => e.Panel == normalizedPanel)
            .OrderBy(e => e.SampleId)
            .ThenBy(e => e.Population.Name)
            .Select(e => new GetPanelSampleLinksContract.Response
            {
                SampleId = e.SampleId,
                PopulationId = e.QpadmPopulationId,
                PopulationName = e.Population.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SetSamplePopulationsContract.Response> SetSamplePopulationsAsync(string identityId,
        SetSamplePopulationsContract.Request request, CancellationToken cancellationToken = default)
    {
        var panel = request.Panel.Trim();
        var sampleId = request.SampleId.Trim();
        var desired = await ValidatePopulationIdsAsync(request.PopulationIds, cancellationToken);

        var existing = await dbContext.QpadmPopulationPanelSamples
            .Where(e => e.Panel == panel && e.SampleId == sampleId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(e => e.QpadmPopulationId).ToHashSet();

        var now = DateTime.UtcNow;
        foreach (var toRemove in existing.Where(e => !desired.Contains(e.QpadmPopulationId)))
            dbContext.QpadmPopulationPanelSamples.Remove(toRemove);

        foreach (var populationId in desired.Where(id => !existingIds.Contains(id)))
            dbContext.QpadmPopulationPanelSamples.Add(NewLink(panel, sampleId, populationId, identityId, now));

        await dbContext.SaveChangesAsync(cancellationToken);

        var populations = await dbContext.QpadmPopulationPanelSamples
            .AsNoTracking()
            .Where(e => e.Panel == panel && e.SampleId == sampleId)
            .OrderBy(e => e.Population.Name)
            .Select(e => new LinkedPopulationDto.Response
            {
                PopulationId = e.QpadmPopulationId,
                PopulationName = e.Population.Name
            })
            .ToListAsync(cancellationToken);

        return new SetSamplePopulationsContract.Response
        {
            Panel = panel,
            SampleId = sampleId,
            Populations = populations
        };
    }

    public async Task<BulkAssignSamplePopulationsContract.Response> BulkAssignAsync(string identityId,
        BulkAssignSamplePopulationsContract.Request request, CancellationToken cancellationToken = default)
    {
        var panel = request.Panel.Trim();
        var sampleIds = request.SampleIds
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();
        var replace = string.Equals(request.Mode, "replace", StringComparison.OrdinalIgnoreCase);
        var desired = await ValidatePopulationIdsAsync(request.PopulationIds, cancellationToken);

        if (sampleIds.Count == 0)
            return new BulkAssignSamplePopulationsContract.Response();

        var existing = await dbContext.QpadmPopulationPanelSamples
            .Where(e => e.Panel == panel && sampleIds.Contains(e.SampleId))
            .ToListAsync(cancellationToken);

        var existingBySample = existing
            .GroupBy(e => e.SampleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.UtcNow;
        var added = 0;
        var removed = 0;

        foreach (var sampleId in sampleIds)
        {
            existingBySample.TryGetValue(sampleId, out var sampleLinks);
            sampleLinks ??= [];
            var existingIds = sampleLinks.Select(e => e.QpadmPopulationId).ToHashSet();

            if (replace)
            {
                foreach (var toRemove in sampleLinks.Where(e => !desired.Contains(e.QpadmPopulationId)))
                {
                    dbContext.QpadmPopulationPanelSamples.Remove(toRemove);
                    removed++;
                }
            }

            foreach (var populationId in desired.Where(id => !existingIds.Contains(id)))
            {
                dbContext.QpadmPopulationPanelSamples.Add(NewLink(panel, sampleId, populationId, identityId, now));
                added++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new BulkAssignSamplePopulationsContract.Response
        {
            SamplesAffected = sampleIds.Count,
            LinksAdded = added,
            LinksRemoved = removed
        };
    }

    /// <summary>Dedupes the requested ids and drops any that don't reference an existing population.</summary>
    private async Task<HashSet<int>> ValidatePopulationIdsAsync(List<int> populationIds,
        CancellationToken cancellationToken)
    {
        var requested = populationIds.Distinct().ToList();
        if (requested.Count == 0) return [];

        var valid = await dbContext.QpadmPopulations
            .AsNoTracking()
            .Where(p => requested.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        return valid.ToHashSet();
    }

    private static QpadmPopulationPanelSample NewLink(string panel, string sampleId, int populationId,
        string identityId, DateTime now) => new()
    {
        Panel = panel,
        SampleId = sampleId,
        QpadmPopulationId = populationId,
        CreatedAt = now,
        CreatedBy = identityId,
        UpdatedAt = now,
        UpdatedBy = identityId
    };
}
