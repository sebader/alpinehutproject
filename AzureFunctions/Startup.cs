using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Net.Http;

[assembly: FunctionsStartup(typeof(AzureFunctions.Startup))]

namespace AzureFunctions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services
                .AddHttpClient<HttpClient>("HttpClient")
                .AddPolicyHandler(GetRetryWithTimeoutPolicy());
        }

        static IAsyncPolicy<HttpResponseMessage> GetRetryWithTimeoutPolicy()
        {
            var timeoutPerCall = Policy.TimeoutAsync(30);

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            return retryPolicy.WrapAsync(timeoutPerCall);
        }
    }
}
