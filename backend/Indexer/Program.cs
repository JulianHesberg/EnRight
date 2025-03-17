using Indexer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

public class Program
{
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IndexerContext>();
            db.Database.EnsureCreated();
        }
        host.Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>()
                          .UseUrls("http://0.0.0.0:5000");
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Read environment variables
                var sqlHost = Environment.GetEnvironmentVariable("SQL_HOST") ?? "mssql";
                var sqlPort = Environment.GetEnvironmentVariable("SQL_PORT") ?? "1433";
                var sqlUser = Environment.GetEnvironmentVariable("SQL_USER") ?? "sa";
                var sqlPass = Environment.GetEnvironmentVariable("SQL_PASSWORD") ?? "Your_password123";

                // Build a valid connection string for Docker Compose
                var connectionString =
                    $"Server={sqlHost},{sqlPort};Database=EnronIndex;User Id={sqlUser};Password={sqlPass};TrustServerCertificate=True;";

                // Register the DbContext with EF Core
                services.AddDbContext<IndexerContext>(options =>
                    options.UseSqlServer(connectionString));

                // Register FileService
                services.AddScoped<FileService>();

                services.AddHostedService<IndexWorker>();
            });
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
        });
    }
}
