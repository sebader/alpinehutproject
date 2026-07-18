using System;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WebsiteBackendFunctions;

public class Program
{
    public static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
            {
                var openTelemetry = services.AddOpenTelemetry()
                    .UseFunctionsWorkerDefaults();

                // Only export to Azure Monitor when a connection string is configured (e.g. in Azure).
                // Locally the exporter throws when APPLICATIONINSIGHTS_CONNECTION_STRING is not set.
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
                {
                    openTelemetry.UseAzureMonitorExporter();
                }
            })
            .Build();

        host.Run();
    }
}