using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Prometheus;
using Serilog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

public class Program
{
    public static void Main(string[] args)
    {
        // 1. Configure Serilog globally
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console() // Structured logging to the console
            .CreateLogger();

        try
        {
            // 2. Build and run the host
            Host.CreateDefaultBuilder(args)
                .UseSerilog() // Use Serilog for logging
                .ConfigureServices((hostContext, services) =>
                {
                    // Read environment variables for database configuration
                    var sqlHost = Environment.GetEnvironmentVariable("SQL_HOST") ?? "mssql";
                    var sqlPort = Environment.GetEnvironmentVariable("SQL_PORT") ?? "1433";
                    var sqlUser = Environment.GetEnvironmentVariable("SQL_USER") ?? "sa";
                    var sqlPass = Environment.GetEnvironmentVariable("SQL_PASSWORD") ?? "Your_password123";

                    // Build a valid connection string
                    var connectionString = 
                        $"Server={sqlHost},{sqlPort};Database=EnronIndex;User Id={sqlUser};Password={sqlPass};TrustServerCertificate=True;";

                    // Register the DbContext for EF Core
                    services.AddDbContext<IndexerContext>(options =>
                        options.UseSqlServer(connectionString));

                    // Register the IndexWorker as a hosted service
                    services.AddHostedService<IndexWorker>();

                    // Add OpenTelemetry for tracing and metrics
                    services.AddOpenTelemetry()
                        .WithTracing(tracerBuilder =>
                        {
                            tracerBuilder
                                .AddSource("Indexer") // Custom ActivitySource from IndexWorker
                                .SetResourceBuilder(
                                    ResourceBuilder.CreateDefault()
                                        .AddService("IndexerService")) // Trace service name
                                .SetSampler(new AlwaysOnSampler()) // Sample all traces
                                .AddHttpClientInstrumentation() // Trace HttpClient calls
                                .AddSqlClientInstrumentation() // Trace SQL queries
                                .AddZipkinExporter(o =>
                                {
                                    o.Endpoint = new Uri("http://zipkin:9411/api/v2/spans"); // Zipkin exporter endpoint
                                });
                        })
                        .WithMetrics(meterBuilder =>
                        {
                            meterBuilder
                                .AddRuntimeInstrumentation() // Runtime metrics (GC, etc.)
                                .SetResourceBuilder(
                                    ResourceBuilder.CreateDefault()
                                        .AddService("IndexerService")); // Metrics service name
                        });

                    // Add Prometheus metric server
                    services.AddMetricServer(options =>
                    {
                        options.Port = 8081; // Expose metrics on port 8081
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();

                        // Register Prometheus metrics middleware
                        app.UseMetricServer();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapMetrics(); // Expose metrics endpoint for Prometheus
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
    }
}
