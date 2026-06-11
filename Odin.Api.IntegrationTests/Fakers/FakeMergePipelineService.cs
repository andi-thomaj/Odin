using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.IntegrationTests.Fakers;

/// <summary>
/// Test double for <see cref="IMergePipelineService"/>. The real one proxies the external tools-api;
/// here only the panel <c>.ind</c> label view/edit methods are backed by an in-memory store so the
/// panel-promotion label flow can be exercised. Registered as a singleton; tests seed rows via
/// <see cref="SetRows"/> and read the result via <see cref="Rows"/>. The heavy merge/restore methods
/// throw — no integration test path uses them.
/// </summary>
public sealed class FakeMergePipelineService : IMergePipelineService
{
    private readonly object _gate = new();
    private List<PanelIndRowResult> _rows = [];
    private string _panel = "HO";

    /// <summary>Seed the in-memory panel. Rows are addressed by their position index.</summary>
    public void SetRows(string panel, IEnumerable<(string Id, string Sex, string Label)> rows)
    {
        lock (_gate)
        {
            _panel = panel;
            _rows = rows
                .Select((r, i) => new PanelIndRowResult(i, r.Id, r.Sex, r.Label))
                .ToList();
        }
    }

    /// <summary>Current snapshot of the in-memory rows (post-edits) for assertions.</summary>
    public IReadOnlyList<PanelIndRowResult> Rows
    {
        get { lock (_gate) return _rows.ToList(); }
    }

    public Task<PanelIndRowsResult> GetPanelIndRowsAsync(string? panel, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(new PanelIndRowsResult(
                _panel, _panel, _rows.Count, _rows.ToList()));
        }
    }

    public Task<PanelIndRowResult> SetPanelIndRowLabelAsync(
        string? panel, int index, string label, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (index < 0 || index >= _rows.Count)
                throw new MergePipelineException(System.Net.HttpStatusCode.UnprocessableEntity,
                    $"index {index} out of range");
            var old = _rows[index];
            var updated = old with { Label = label };
            _rows[index] = updated;
            return Task.FromResult(updated);
        }
    }

    // ── Unused by the promotion tests ──────────────────────────────────────────────
    public Task<ConvertResult> ConvertAsync(byte[] raw, string fileName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MergeResult> RunMergeAsync(string mergeId, string converted23Andme, string? panel, string? sampleId,
        string sex, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<HttpResponseMessage> OpenDownloadAsync(string mergeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteAsync(string mergeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PanelStatusResult> GetPanelStatusAsync(string? panel, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PanelUploadResult> UploadPanelFileAsync(string ext, string? panel, string? sha256, Stream body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PanelActivateResult> ActivatePanelAsync(string? panel, bool force,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PanelRenameLabelResult> RenamePanelLabelAsync(string? panel, string fromLabel, string toLabel,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
