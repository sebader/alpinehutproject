using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions.Functions;

public static class DurableSequentialProcessor
{
    /// <summary>
    /// Processes one activity call per hut id sequentially (one hut at a time), pausing for a short delay
    /// (default 1 second) between huts to stay gentle on the provider. Each provider now returns a hut's
    /// availability in a single request (no more per-day/per-page paging), so the previous heavy batching is
    /// no longer needed. Shared by the AV and HuettenHoliday availability orchestrators.
    /// </summary>
    /// <typeparam name="TResult">Activity return type (deserialized then discarded).</typeparam>
    public static async Task ProcessSequentiallyAsync<TResult>(
        this TaskOrchestrationContext context,
        IReadOnlyList<int> hutIds,
        string activityName,
        ILogger logger,
        int delaySeconds = 1)
    {
        for (var i = 0; i < hutIds.Count; i++)
        {
            var hutId = hutIds[i];
            logger.LogInformation("Processing {ActivityName} for hutId={HutId} ({Index}/{Total})",
                activityName, hutId, i + 1, hutIds.Count);
            await context.CallActivityAsync<TResult>(activityName, hutId);

            // Pause between huts (but not after the last one) to avoid hammering the provider.
            if (i < hutIds.Count - 1)
            {
                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(delaySeconds), CancellationToken.None);
            }
        }

        logger.LogInformation("All {Total} {ActivityName} activities finished", hutIds.Count, activityName);
    }
}
