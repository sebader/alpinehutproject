using System.Net.Http.Headers;
using Azure.Monitor.OpenTelemetry.Exporter;
using FetchDataFunctions.Models;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace FetchDataFunctions;

public static class Program
{
    public static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices((context, services) =>
            {
                var openTelemetry = services.AddOpenTelemetry()
                    .UseFunctionsWorkerDefaults();

                // Only export to Azure Monitor when a connection string is configured (e.g. in Azure).
                // Locally the exporter throws when APPLICATIONINSIGHTS_CONNECTION_STRING is not set.
                if (!string.IsNullOrEmpty(context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
                {
                    openTelemetry.UseAzureMonitorExporter();
                }

                // Injectable clock so time-dependent logic (availability windows, month iteration) is testable.
                services.AddSingleton(TimeProvider.System);

                // One short-lived DbContext per activity. A pooled factory is the right fit for the Durable
                // fan-out (many concurrent activities): a shared scoped/singleton context would not be thread-safe.
                services.AddPooledDbContextFactory<AlpinehutsDbContext>(options =>
                    options.UseSqlServer(
                        context.Configuration["DatabaseConnectionString"],
                        sql => sql.EnableRetryOnFailure()));

                services
                    .AddHttpClient("HttpClient", client =>
                    {
                        var productValue = new ProductInfoHeaderValue("HutInfoScraperBot", "1.0");
                        client.DefaultRequestHeaders.UserAgent.Add(productValue);

                        // Overall request timeout (includes all Polly retries).
                        client.Timeout = TimeSpan.FromSeconds(120);
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        // Prevent cookie sharing in multi-threaded env
                        // https://learn.microsoft.com/aspnet/core/fundamentals/http-requests#cookies
                        UseCookies = false,
                    })
                    .AddPolicyHandler(GetRetryWithTimeoutPolicy());
            })
            .Build();

        host.Run();
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryWithTimeoutPolicy()
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            // Retry 429 (rate limiting) and 403 (also observed as rate limiting on the providers).
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.Forbidden)
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            .WrapAsync(Policy.TimeoutAsync(30)); // per-request timeout

        return retryPolicy;
    }
}
