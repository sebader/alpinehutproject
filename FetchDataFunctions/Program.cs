using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace FetchDataFunctions;

public class Program
{
    public static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureLogging(logging =>
            {
                logging.Services.Configure<LoggerFilterOptions>(options =>
                {
                    // Remove the default rule added by the worker service
                    // https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#managing-log-levels
                    var defaultRule = options.Rules.FirstOrDefault(rule => rule.ProviderName
                                                                           == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
                    if (defaultRule is not null)
                    {
                        options.Rules.Remove(defaultRule);
                    }
                });
            })
            .ConfigureServices(services =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                services
                    .AddHttpClient<HttpClient>("HttpClient", client =>
                    {
                        var productValue = new ProductInfoHeaderValue("HutInfoScraperBot", "1.0");
                        client.DefaultRequestHeaders.UserAgent.Add(productValue);

                        client.Timeout =
                            TimeSpan.FromSeconds(
                                120); // default overall request request timeout (includes all polly retries)
                    })
                    .ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        return new HttpClientHandler()
                        {
                            UseCookies =
                                false, // Prevent cookie sharing in multi thread env https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0#cookies
                        };
                    })
                    .AddPolicyHandler(GetRetryWithTimeoutPolicy());
            })
            .Build();

        host.Run();
    }

    static IAsyncPolicy<HttpResponseMessage> GetRetryWithTimeoutPolicy()
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg =>
                msg.StatusCode ==
                System.Net.HttpStatusCode.Forbidden) // Retry 403 as it seems to be some rate limiting error
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            .WrapAsync(Policy.TimeoutAsync(30)); // per request timeout

        return retryPolicy;
    }
}