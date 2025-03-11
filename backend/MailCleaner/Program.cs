using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register our MailCleanerWorker as a hosted service
        services.AddHostedService<MailCleanerWorker>();

        services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService("MailCleanerService"))
            // If you do any HTTP calls, .AddHttpClientInstrumentation()
            .AddZipkinExporter(o =>
            {
                // If running Zipkin in Docker Compose:
                // 'zipkin' is the container name, port 9411
                o.Endpoint = new Uri("http://zipkin:9411/api/v2/spans");
            });
    });
    })
    .Build()
    .Run();