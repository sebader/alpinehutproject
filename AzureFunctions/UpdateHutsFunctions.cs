using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace AzureFunctions
{
    public static class UpdateHutsFunctions
    {
        private const int MaxHutId = 300;
        private const int ParallelTasks = 100;

        private static HttpClient _httpClient = new HttpClient();

        [FunctionName("UpdateHutsTimerTriggered")]
        public static async Task UpdateHutsTimerTriggered([TimerTrigger("0 0 0 * * 0")]TimerInfo myTimer,
            ILogger log,
            [OrchestrationClient] DurableOrchestrationClient starter)
        {
            //var res = await GetHutFromProviderActivity(113, log);
            string instanceId = await starter.StartNewAsync("UpdateHutsOrchestrator", 1);
            log.LogInformation($"UpdateHut orchestrator started. Instance ID={instanceId}");
        }

        [FunctionName("UpdateHutsOrchestrator")]
        public static async Task UpdateHutsOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            int startHutId = context.GetInput<int>();
            if (!context.IsReplaying)
            {
                log.LogInformation($"Starting orchestrator with startHutId={startHutId}");
            }
            var tasks = new Task<Tuple<int, Hut>>[ParallelTasks];

            // Fan-out
            for (int i = 0; i < ParallelTasks; i++)
            {
                tasks[i] = context.CallActivityAsync<Tuple<int, Hut>>("GetHutFromProvider", i + startHutId);
            }

            await Task.WhenAll(tasks);

            var hutList = tasks.Select(t => t.Result).Where(r => r.Item2 != null).Select(i => i.Item2).ToList();

            var notFoundIds = tasks.Select(t => t.Result).Where(r => r.Item2 == null).Select(i => i.Item1).ToList();
            if (!context.IsReplaying && notFoundIds.Count > 0)
            {
                log.LogWarning($"Did not find huts for the following IDs={string.Join(',', notFoundIds)}");
            }

            var databaseUpdates = await context.CallActivityAsync<int>("UpsertHuts", hutList);
            if (!context.IsReplaying)
            {
                log.LogDebug($"Database entries written={databaseUpdates}");
            }

            int nextStartId = startHutId + ParallelTasks;

            if (nextStartId <= MaxHutId)
            {
                context.ContinueAsNew(nextStartId);
            }
            else
            {
                log.LogInformation($"MaxHutId {MaxHutId} reached. Ending hut updating now.");
            }
        }


        [FunctionName("GetHutFromProvider")]
        public static async Task<Tuple<int, Hut>> GetHutFromProviderActivity([ActivityTrigger] int hutId, ILogger log)
        {
            Hut hut = null;
            try
            {
                var response = await _httpClient.GetAsync($"{Helpers.HutProviderBaseUrl}{hutId}");

                var responseBody = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(responseBody) && !responseBody.Contains("kann nicht gefunden werden"))
                {
                    hut = Helpers.ParseHutInformation(responseBody, log);
                    if (hut != null)
                    {
                        hut.Id = hutId;
                        hut.Link = $"{Helpers.HutProviderBaseUrl}{hutId}";
                    }
                    else
                    {
                        log.LogError($"Error parsing hut page for ID={hutId}");
                    }
                }
                else
                {
                    log.LogInformation($"Hut with ID={hutId} not found");
                }
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in http call to provider");
            }
            return new Tuple<int, Hut>(hutId, hut);
        }

        [FunctionName("UpsertHuts")]
        public static async Task<int> UpsertHuts([ActivityTrigger] IList<Hut> huts, ILogger log)
        {
            try
            {
                var dbContext = Helpers.GetDbContext();

                foreach (var hut in huts)
                {
                    var existingHut = await dbContext.Huts.FirstOrDefaultAsync(h => h.Id == hut.Id);
                    if (existingHut != null)
                    {
                        log.LogInformation($"Updating existing hut ID={hut.Id} name={hut.Name}");
                        existingHut.Name = hut.Name;
                        existingHut.Link = hut.Link;
                        existingHut.Coordinates = hut.Coordinates;
                        existingHut.Country = hut.Country;
                        existingHut.Enabled = hut.Enabled;
                        existingHut.LastUpdated = hut.LastUpdated;
                        dbContext.Update(existingHut);
                    }
                    else
                    {
                        log.LogInformation($"Adding new hut ID={hut.Id} name={hut.Name}");
                        dbContext.Huts.Add(hut);
                    }
                }

                return await dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in writing hut updates to database");
                return -1;
            }
        }
    }
}
