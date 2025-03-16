using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Prometheus;
using Serilog;

// 1. Configure Serilog globally
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console() // structured logs to console
    .CreateLogger();

try
{
    // 2. Create and run the Host
    Host.CreateDefaultBuilder(args)
        // Use Serilog for logging
        .UseSerilog()

        // 3. Add ASP.NET Core so we can host the /metrics endpoint
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.ConfigureServices(services =>
            {
                // Register our MailCleanerWorker as a hosted service
                services.AddHostedService<MailCleanerWorker>();

                // Add OpenTelemetry for Traces + Metrics
                services.AddOpenTelemetry()
                    .WithTracing(tracerBuilder =>
                    {
                        tracerBuilder
                            .AddSource("MailCleaner")  // If you have a custom ActivitySource in MailCleaner
                            .SetResourceBuilder(
                                ResourceBuilder.CreateDefault()
                                    .AddService("MailCleanerService"))
                            // Sample everything
                            .SetSampler(new AlwaysOnSampler())
                            .AddHttpClientInstrumentation()
                            .AddZipkinExporter(o =>
                            {
                                // Send traces to Zipkin at port 9411
                                o.Endpoint = new Uri("http://zipkin:9411/api/v2/spans");
                            });
                    })
                    .WithMetrics(meterBuilder =>
                    {
                        meterBuilder
                            // Collect .NET runtime metrics (GC, CPU, etc.)
                            .AddRuntimeInstrumentation()
                            .SetResourceBuilder(
                                ResourceBuilder.CreateDefault()
                                    .AddService("MailCleanerService"));
                    });

                services.AddMetricServer(options =>
                {
                    
                });
            });

            webBuilder.Configure(app =>
            {
                app.UseRouting();

                // Register Prometheus metrics middleware
                app.UseMetricServer();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapMetrics(); // Expose the /metrics endpoint
                });
            });
        })

        .Build()
        .Run();
}
finally
{
    Log.CloseAndFlush();
}
