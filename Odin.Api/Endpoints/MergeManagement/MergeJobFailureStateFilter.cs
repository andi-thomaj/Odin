using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.MergeManagement
{
    /// <summary>
    /// Reconciles a merge order's DB status to <see cref="MergeStatus.Failed"/> when its Hangfire job
    /// finally fails (retries exhausted). <see cref="MergeJob.RunAsync"/> records transient failures as
    /// <see cref="MergeStatus.Retrying"/> and rethrows; once Hangfire gives up and moves the job into
    /// FailedState, this filter flips Retrying → Failed so the row doesn't stay stuck as Retrying and
    /// permanently consume an in-flight merge slot (which would eventually deadlock the bounded queue).
    /// </summary>
    public sealed class MergeJobFailureStateFilter(IServiceScopeFactory scopeFactory) : IApplyStateFilter
    {
        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            if (context.NewState is not FailedState)
                return; // only when Hangfire has exhausted retries (not on the per-attempt scheduled retry)

            var job = context.BackgroundJob.Job;
            if (job is null
                || job.Type != typeof(IMergeJob)
                || job.Method.Name != nameof(IMergeJob.RunAsync)
                || job.Args.Count == 0
                || job.Args[0] is not int inspectionId)
                return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fileId = db.QpadmGeneticInspections
                .IgnoreQueryFilters()
                .Where(gi => gi.Id == inspectionId)
                .Select(gi => (int?)gi.RawGeneticFileId)
                .FirstOrDefault();
            if (fileId is null)
                return;

            // Flip any non-terminal in-flight status → Failed (Retrying is the usual case; Queued/
            // Converting/Merging covers a job that died before the transient catch ran). Ready/Deleted/
            // Failed/NotStarted are left untouched so a success or terminal write is never clobbered.
            // ExecuteUpdate avoids loading the (large) RawData blob.
            db.RawGeneticFiles
                .IgnoreQueryFilters()
                .Where(f => f.Id == fileId.Value && (
                    f.MergeStatus == MergeStatus.Retrying
                    || f.MergeStatus == MergeStatus.Queued
                    || f.MergeStatus == MergeStatus.Converting
                    || f.MergeStatus == MergeStatus.Merging))
                .ExecuteUpdate(s => s.SetProperty(f => f.MergeStatus, MergeStatus.Failed));
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }
    }
}
