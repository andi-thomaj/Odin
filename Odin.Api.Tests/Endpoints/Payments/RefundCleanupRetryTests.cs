using System.Reflection;
using Hangfire;
using Odin.Api.Endpoints.Payments;
using Xunit;

namespace Odin.Api.Tests.Endpoints.Payments;

/// <summary>
/// Regression guard for the deploy-interruption bug class applied to the destructive refund purge. The job is
/// idempotent (covered by <c>RefundCleanupJobTests.Purge_IsIdempotent</c>), so it MUST be retried after a worker
/// death / redeploy mid-run — otherwise a partial purge strands the order's PRIVATE R2 portrait images, which the
/// daily <c>users/{slug}</c> sweep won't reclaim (the user still exists after a refund). This pins the retry policy
/// on the INTERFACE method (where Hangfire reads it) so it can't silently revert to <c>Attempts = 0</c>.
/// </summary>
public class RefundCleanupRetryTests
{
    [Fact]
    public void PurgeRefundedOrder_RetriesAfterInterruption_FiltersLiveOnTheInterface()
    {
        var purge = typeof(IRefundCleanupJob).GetMethod(nameof(IRefundCleanupJob.PurgeRefundedOrderAsync))!;

        Assert.Equal("default", purge.GetCustomAttribute<QueueAttribute>()?.Queue);

        var retry = purge.GetCustomAttribute<AutomaticRetryAttribute>();
        Assert.NotNull(retry);
        Assert.True(retry!.Attempts >= 1, "an idempotent destructive purge must retry after a worker death, not be lost");
        Assert.Equal(AttemptsExceededAction.Fail, retry.OnAttemptsExceeded); // surface as Failed for an admin, not Deleted
    }
}
