using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

[assembly: FunctionsStartup(typeof(FetchDataFunctions.Startup))]

namespace FetchDataFunctions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services
                .AddHttpClient<HttpClient>("HttpClient", client =>
                {
                    var productValue = new ProductInfoHeaderValue("HutInfoScraperBot", "1.0");
                    client.DefaultRequestHeaders.UserAgent.Add(productValue);

                    client.Timeout = TimeSpan.FromSeconds(120); // default overall request request timeout (includes all polly retries)
                })
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new HttpClientHandler()
                    {
                        UseCookies = false, // Prevent cookie sharing in multi thread env https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0#cookies
                    };
                })
                .AddPolicyHandler(GetRetryWithTimeoutPolicy());
        }

        static IAsyncPolicy<HttpResponseMessage> GetRetryWithTimeoutPolicy()
        {
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.Forbidden) // Retry 403 as it seems to be some rate limiting error
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
                .WrapAsync(Policy.TimeoutAsync(30)); // per request timeout

            return retryPolicy;
        }
    }
}
