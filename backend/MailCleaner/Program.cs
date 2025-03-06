using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register our MailCleanerWorker as a hosted service
        services.AddHostedService<MailCleanerWorker>();
    })
    .Build()
    .Run();