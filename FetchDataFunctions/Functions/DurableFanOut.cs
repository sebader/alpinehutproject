using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions.Functions;

public static class DurableFanOut
{
    /// <summary>
    /// Fans out one activity call per hut id. To avoid provider rate limiting, calls are issued in batches
    /// (default 10) with a delay (default 1 minute) between batches, waiting for each batch to finish before
    /// starting the next. Shared by the AV and HuettenHoliday availability orchestrators.
    /// </summary>
    /// <typeparam name="TResult">Activity return type (deserialized then discarded).</typeparam>
    public static async Task FanOutInBatchesAsync<TResult>(
        this TaskOrchestrationContext context,
        IReadOnlyList<int> hutIds,
        string activityName,
        ILogger logger,
        int batchSize = 10,
        int delayMinutes = 1)
    {
        var tasks = new List<Task>();

        foreach (var hutId in hutIds)
        {
            logger.LogInformation("Scheduling {ActivityName} for hutId={HutId}", activityName, hutId);
            tasks.Add(context.CallActivityAsync<TResult>(activityName, hutId));

            if (tasks.Count >= batchSize)
            {
                logger.LogInformation("Delaying next batch for {DelayMinutes} minute(s), last hutId={HutId}", delayMinutes, hutId);
                await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(delayMinutes), CancellationToken.None);

                logger.LogInformation("Waiting for batch of {ActivityName} activities to finish", activityName);
                await Task.WhenAll(tasks);
                logger.LogInformation("Finished batch");

                tasks.Clear();
            }
        }

        logger.LogInformation("All {ActivityName} activities scheduled. Waiting for the last batch to finish", activityName);
        await Task.WhenAll(tasks);
    }
}
