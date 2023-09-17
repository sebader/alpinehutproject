using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace WebsiteBackendFunctions.WebsiteFunctions
{
    public class CleanupFreeBedNotificationSubscriptions
    {
        /// <summary>
        /// On execution, it runs the cleanup stored procedure in the database which removes
        /// any free bed notification subscriptions which are now in the past
        /// </summary>
        /// <param name="myTimer"></param>
        /// <param name="result"></param>
        /// <param name="log"></param>
        [FunctionName(nameof(CleanupFreeBedNotificationSubscriptions))]
        public void Run([TimerTrigger("0 0 1 * * * ")]TimerInfo myTimer,
            [Sql("[dbo].[DeleteOldFreeBedSubscriptions]",
            "DatabaseConnectionString",
            CommandType.StoredProcedure)] IEnumerable<CleanupFreeBedNotificationSubscriptions> result, 
            ILogger log)
        {
            log.LogInformation($"Free bed subscription cleanup timer trigger function executed at: {DateTime.Now}");
            log.LogInformation($"Now {result.Count()} subscriptions are left in the database");
        }
    }
}
