using System.Data;
using FetchDataFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions;

public class CleanupFreeBedNotificationSubscriptions(ILogger<CleanupFreeBedNotificationSubscriptions> logger)
{
    /// <summary>
    /// On execution, it runs the cleanup stored procedure in the database which removes
    /// any free bed notification subscriptions which are now in the past.
    /// </summary>
    [Function(nameof(CleanupFreeBedNotificationSubscriptions))]
    public void Run([TimerTrigger("%CleanupSchedule%")] TimerInfo myTimer,
        [SqlInput("[dbo].[DeleteOldFreeBedSubscriptions]",
            "DatabaseConnectionString",
            CommandType.StoredProcedure, "")]
        IEnumerable<FreeBedUpdateSubscription> result)
    {
        logger.LogInformation("Free bed subscription cleanup timer trigger function executed");
        logger.LogInformation("Now {SubscriptionCount} subscriptions are left in the database", result.Count());
    }
}
