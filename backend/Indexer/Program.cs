using Indexer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Prometheus;
using Serilog;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    // Database connection logic
                    var sqlHost = Environment.GetEnvironmentVariable("SQL_HOST") ?? "mssql";
                    var sqlPort = Environment.GetEnvironmentVariable("SQL_PORT") ?? "1433";
                    var sqlUser = Environment.GetEnvironmentVariable("SQL_USER") ?? "sa";
                    var sqlPass = Environment.GetEnvironmentVariable("SQL_PASSWORD") ?? "Your_password123";
                    var connectionString =
                        $"Server={sqlHost},{sqlPort};Database=EnronIndex;User Id={sqlUser};Password={sqlPass};TrustServerCertificate=True;";

                    services.AddDbContext<IndexerContext>(options =>
                        options.UseSqlServer(connectionString));
                    services.AddScoped<FileService>();
                    services.AddHostedService<IndexWorker>();

                    // Add OpenTelemetry
                    services.AddOpenTelemetry()
                        .WithTracing(tracerBuilder =>
                        {
                            tracerBuilder
                                .AddSource("Indexer")
                                .AddSource("Indexer.FileController")
                                .SetResourceBuilder(
                                    ResourceBuilder.CreateDefault()
                                        .AddService("IndexerService"))
                                .SetSampler(new AlwaysOnSampler())
                                .AddHttpClientInstrumentation()
                                .AddSqlClientInstrumentation()
                                .AddZipkinExporter(o =>
                                {
                                    o.Endpoint = new Uri("http://zipkin:9411/api/v2/spans");
                                });
                        })
                        .WithMetrics(meterBuilder =>
                        {
                            meterBuilder
                                .AddRuntimeInstrumentation()
                                .SetResourceBuilder(
                                    ResourceBuilder.CreateDefault()
                                        .AddService("IndexerService"));
                        });
                    services.AddMetricServer(options =>
                    {
                        options.Port = 8081;
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                              .UseUrls("http://0.0.0.0:5000");
                })
                .Build();

            host.Run();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors("AllowAll");

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapMetrics(); // Expose Prometheus metrics
        });

        app.UseMetricServer(); // Serve metrics on port 8081
    }
}
