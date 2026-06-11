using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Odin.Api.HealthChecks
{
    /// <summary>
    /// Reports whether the Hangfire job storage is queryable (its schema exists and is readable).
    /// Distinct from the raw Postgres check: it confirms the background-job subsystem is usable, not
    /// just that the database accepts connections. Registered as <see cref="HealthStatus.Degraded"/> so
    /// a job-storage problem is surfaced without taking the whole API out of the load-balancer rotation.
    /// </summary>
    public sealed class HangfireStorageHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = JobStorage.Current.GetMonitoringApi().GetStatistics();
                return Task.FromResult(HealthCheckResult.Healthy(
                    $"hangfire storage ok (servers={stats.Servers}, enqueued={stats.Enqueued}, failed={stats.Failed})"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Degraded("hangfire storage unavailable", ex));
            }
        }
    }
}
