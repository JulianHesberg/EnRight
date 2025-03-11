using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using MailCleaner.Models;
using System.Diagnostics;

public class MailCleanerWorker : BackgroundService
{
    // RabbitMQ connection
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly ActivitySource ActivitySource = new("MailCleaner");

    // Directories for raw & processed emails
    private readonly string _emailDirectory = "maildir";
    private readonly string _processedDirectory = "processed";

    public MailCleanerWorker()
    {

        var hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var port = Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672";
        // Configure RabbitMQ client
        _factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = int.Parse(port),
            UserName = "guest",
            Password = "guest"
        };
    }

    // Called when the Worker starts
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Create asynchronous connection & channel
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

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


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ProcessMailDirectory(_emailDirectory, _processedDirectory);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
    private async void ProcessMailDirectory(string mailDir, string processedDir)
    {
        foreach (var nameDir in Directory.GetDirectories(mailDir))
        {
            string name = Path.GetFileName(nameDir);
            string nameProcessedDir = Path.Combine(processedDir, name);

            foreach (var typeDir in Directory.GetDirectories(nameDir))
            {
                string type = Path.GetFileName(typeDir);
                string typeProcessedDir = Path.Combine(nameProcessedDir, type);

                Directory.CreateDirectory(typeProcessedDir);

                foreach (var emailFile in Directory.GetFiles(typeDir))
                {
                    try
                    {
                        byte[] fileData = File.ReadAllBytes(emailFile);
                        string cleanedData = CleanEmail(fileData);

                        string newFileName = $"{name}_{type}_{Path.GetFileName(emailFile)}";
                        string newFilePath = Path.Combine(typeProcessedDir, Path.GetFileName(emailFile));

                        Directory.CreateDirectory(typeProcessedDir);
                        File.Move(emailFile, newFilePath);

                        var body = new CleanedEmail();
                        body.FileName = newFileName;
                        body.Content = cleanedData;
                        body.Data = fileData;

                        await PublishMessageAsync(body);

                        Console.WriteLine($"Processed: {emailFile}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing {emailFile}: {ex.Message}");
                    }
                }
            }
        }
    }

    // TODO (Temp implementation for cleaning) refactor to match our own needs.
    private string CleanEmail(byte[] rawContent)
    {
        // Convert byte array to string using UTF-8 encoding
        string emailContent = Encoding.UTF8.GetString(rawContent);

        // Split the email content into lines
        var lines = emailContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        bool isBody = false;

        // Loop through each line and remove headers
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                // An empty line indicates the end of headers and the start of the body
                isBody = true;
                continue;
            }
            if (isBody)
            {
                // Append the line to the StringBuilder if it is part of the body
                sb.AppendLine(line);
            }
        }

        // Return the cleaned email content as a string
        return sb.ToString().Trim();
    }

    // Publish cleaned email text to RabbitMQ
    private async Task PublishMessageAsync(CleanedEmail data)
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");

        // 1. Start an OpenTelemetry Activity for this publish operation
        using var activity = ActivitySource.StartActivity("Publish to RabbitMQ", ActivityKind.Producer);

        // 2. Convert CleanedEmail to JSON
        var json = JsonSerializer.Serialize(data);
        var body = Encoding.UTF8.GetBytes(json);

        // 3. Create a new BasicProperties instance manually
        var props = new BasicProperties
        {
            Headers = new Dictionary<string, object?>()
        };

        // 4. Inject trace context for cross-service correlation
        var traceParent = activity?.Id ?? "";
        props.Headers["traceparent"] = Encoding.UTF8.GetBytes(traceParent);

        // 5. **Important**: Pass `props` to `BasicPublishAsync`
        await _channel.BasicPublishAsync(
            exchange: "",
            routingKey: "cleaned_emails",
            mandatory: false,
            basicProperties: props,
            body: body
        );

        Console.WriteLine(" [x] Sent cleaned email with trace context");
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
