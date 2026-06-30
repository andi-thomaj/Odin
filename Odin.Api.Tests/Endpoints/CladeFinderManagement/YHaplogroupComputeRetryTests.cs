using System.Reflection;
using Hangfire;
using Odin.Api.Endpoints.CladeFinderManagement;
using Xunit;

namespace Odin.Api.Tests.Endpoints.CladeFinderManagement;

/// <summary>
/// Regression guard for the Y-DNA clade compute job's deploy-survival policy. It is enqueued via
/// <c>Enqueue&lt;IYHaplogroupComputeService&gt;(..)</c>, so Hangfire reads <c>[AutomaticRetry]</c> off the INTERFACE
/// method — an attribute on the concrete class is silently ignored, which previously left the job on Hangfire's default
/// of 10 retries instead of the intended 3. Pin it on the interface so a worker death / redeploy is retried a bounded
/// number of times and then surfaces as <c>Unavailable</c> (re-enqueued on the next result view).
/// </summary>
public class YHaplogroupComputeRetryTests
{
    [Fact]
    public void ComputeAndPersist_RetryPolicy_LivesOnTheInterface()
    {
        var method = typeof(IYHaplogroupComputeService)
            .GetMethod(nameof(IYHaplogroupComputeService.ComputeAndPersistAsync))!;

        var retry = method.GetCustomAttribute<AutomaticRetryAttribute>();
        Assert.NotNull(retry);
        Assert.Equal(3, retry!.Attempts);
        Assert.Equal(AttemptsExceededAction.Fail, retry.OnAttemptsExceeded);
    }
}
