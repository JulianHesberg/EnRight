using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class MailCleanerWorker : BackgroundService
{
    // RabbitMQ connection
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;

    // Directories for raw & processed emails
    private readonly string _emailDirectory = "emails";
    private readonly string _processedDirectory = "processed";

    public MailCleanerWorker()
    {
        // Configure RabbitMQ client
        _factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest"
        };
    }

    // Called when the Worker starts
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Create asynchronous connection & channel
        _connection = await _factory.CreateConnectionAsync();
        _channel    = await _connection.CreateChannelAsync();

        // Declare the queue where we'll publish cleaned emails
        await _channel.QueueDeclareAsync(
            queue: "cleaned_emails",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken
        );

        // Ensure local directories exist
        Directory.CreateDirectory(_emailDirectory);
        Directory.CreateDirectory(_processedDirectory);

        await base.StartAsync(cancellationToken);
    }

    // Main worker loop
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Find all .txt files in "emails" folder
            var files = Directory.GetFiles(_emailDirectory, "*.txt");

            foreach (var file in files)
            {
                try
                {
                    // Read raw email content
                    string rawContent = await File.ReadAllTextAsync(file, stoppingToken);
                    
                    // Clean out headers
                    string cleanedContent = CleanEmail(rawContent);

                    // Publish cleaned email content to RabbitMQ
                    await PublishMessageAsync(cleanedContent, stoppingToken);

                    // Move the file to "processed" folder
                    string destFile = Path.Combine(_processedDirectory, Path.GetFileName(file));
                    File.Move(file, destFile);

                    Console.WriteLine($"Processed: {file}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {file}: {ex.Message}");
                }
            }

            // Wait 5 seconds before checking for new files
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    // TODO (Temp implementation for cleaning) refactor to match our own needs.
    private string CleanEmail(string rawContent)
    {
        var lines = rawContent.Split('\n');
        var sb = new StringBuilder();
        bool isBody = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                isBody = true;
                continue;
            }
            if (isBody)
            {
                sb.AppendLine(line.TrimEnd('\r'));
            }
        }

        return sb.ToString().Trim();
    }

    // Publish cleaned email text to RabbitMQ
    private async Task PublishMessageAsync(string message, CancellationToken ct)
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            exchange: "",
            routingKey: "cleaned_emails",
            mandatory: false,
            body: body
        );

        Console.WriteLine(" [x] Sent cleaned email to queue");
    }

    // Called when the Worker stops
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.CloseAsync();
        _channel?.Dispose();
        _connection?.CloseAsync();
        _connection?.Dispose();

        return base.StopAsync(cancellationToken);
    }
}
