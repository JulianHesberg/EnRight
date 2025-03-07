using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
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

                services.AddHostedService<IndexWorker>();

            });
}