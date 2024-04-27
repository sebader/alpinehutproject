using System.Collections.Generic;
using System.Data;
using System.Linq;
using FetchDataFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions
{
    public class CleanupFreeBedNotificationSubscriptions
    {
        private readonly ILogger<CleanupFreeBedNotificationSubscriptions> _logger;

        public CleanupFreeBedNotificationSubscriptions(ILogger<CleanupFreeBedNotificationSubscriptions> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// On execution, it runs the cleanup stored procedure in the database which removes
        /// any free bed notification subscriptions which are now in the past
        /// </summary>
        /// <param name="myTimer"></param>
        /// <param name="result"></param>
        [Function(nameof(CleanupFreeBedNotificationSubscriptions))]
        public void Run([TimerTrigger("0 0 1 * * * ")] TimerInfo myTimer,
            [SqlInput("[dbo].[DeleteOldFreeBedSubscriptions]",
                "DatabaseConnectionString",
                CommandType.StoredProcedure, "")]
            IEnumerable<FreeBedUpdateSubscription> result)
        {
            _logger.LogInformation("Free bed subscription cleanup timer trigger function executed");
            _logger.LogInformation($"Now {result.Count()} subscriptions are left in the database");
        }
    }
}