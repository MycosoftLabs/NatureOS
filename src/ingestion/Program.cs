using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Configuration;
using Azure;
using Microsoft.Azure.Functions.Worker;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Add Configuration
        var configuration = context.Configuration;

        // Add Azure services
        services.AddSingleton(provider =>
        {
            var connectionString = configuration.GetConnectionString("CosmosDbConnectionString") ?? 
                                   configuration["CosmosDbConnectionString"];
            return new CosmosClient(connectionString);
        });

        services.AddSingleton(provider =>
        {
            var connectionString = configuration.GetConnectionString("ServiceBusConnectionString") ?? 
                                   configuration["ServiceBusConnectionString"];
            return new ServiceBusClient(connectionString);
        });

        services.AddSingleton(provider =>
        {
            var endpoint = configuration.GetConnectionString("EventGridConnectionString") ??
                           configuration["EventGridConnectionString"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("EventGrid connection string is not configured");
            }

            var key = configuration["EventGridKey"] ?? string.Empty;
            return new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential(key));
        });

        // Add Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run(); 