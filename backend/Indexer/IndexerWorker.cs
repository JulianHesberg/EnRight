
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Indexer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class IndexWorker : BackgroundService
{

    private readonly IServiceProvider _serviceProvider;
    private IConnection? _connection;
    private IChannel? _channel;  // For RabbitMQ communication

    // Create an ActivitySource for the Indexer
    private static readonly ActivitySource ActivitySource = new ActivitySource("Indexer");


    public IndexWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var port = Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672";
        // Configure RabbitMQ connection
        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = int.Parse(port),
            UserName = "guest",
            Password = "guest"
        };

        // Create async connection and channel
        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        // Declare the queue from which we will consume
        await _channel.QueueDeclareAsync(
            queue: "cleaned_emails",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken
        );

        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Create the AsyncEventingBasicConsumer
        var consumer = new AsyncEventingBasicConsumer(_channel);

        // 2. Handle messages asynchronously
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                // (A) Optionally extract the traceparent here
                // ActivityContext parentContext = default;
                // if (ea.BasicProperties.Headers != null &&
                //     ea.BasicProperties.Headers.TryGetValue("traceparent", out object traceParentObj))
                // {
                //     if (traceParentObj is byte[] traceParentBytes)
                //     {
                //         var traceParent = Encoding.UTF8.GetString(traceParentBytes);
                //         parentContext = ActivityContext.Parse(traceParent, null);
                //     }
                // }

                // // (B) Start an Activity for this consume operation
                // using var activity = ActivitySource.StartActivity(
                //     "RabbitMQ Consume",
                //     ActivityKind.Consumer,
                //     parentContext);

                // (C) Process the message
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // e.g. deserialize + index
                var cleanedEmail = JsonSerializer.Deserialize<CleanedEmail>(message);
                if (cleanedEmail != null)
                {
                    await IndexEmailAsync(cleanedEmail);
                }

                // (D) Manually acknowledge
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        };

        // 3. Actually consume messages
        //    Make sure the queue name matches exactly
        _channel.BasicConsumeAsync(
            queue: "cleaned_emails",
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("Indexer is now listening for messages on 'cleaned_emails'...");
        return Task.CompletedTask;
    }


    private async Task IndexEmailAsync(CleanedEmail cleanedEmail)
    {
        // Create a scope to resolve the DbContext via DI
        using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexerContext>();

        // 1. Insert a file record to store the content
        var fileRecord = new FileRecord
        {
            FileName = cleanedEmail.FileName,
            Content = cleanedEmail.Data,
        };
        db.Files.Add(fileRecord);
        await db.SaveChangesAsync();

        // 2. Split the message content into words
        var tokens = cleanedEmail.Content.Split(
            new[] { ' ', '\r', '\n', '\t', ',', '.', ';', ':', '!', '?', '\"', '\'' },
            StringSplitOptions.RemoveEmptyEntries
        );

        // 3. Count occurrences
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var wordStr = token.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(wordStr)) continue;

            if (!wordCounts.ContainsKey(wordStr))
                wordCounts[wordStr] = 0;
            wordCounts[wordStr]++;
        }

        // 4. Upsert words + insert occurrences
        foreach (var kvp in wordCounts)
        {
            // Check if the word already exists
            var existingWord = await db.Words.FirstOrDefaultAsync(w => w.Word == kvp.Key);
            if (existingWord == null)
            {
                existingWord = new Words { Word = kvp.Key };
                db.Words.Add(existingWord);
                await db.SaveChangesAsync();  // get the WordId
            }

            var occurrence = new Occurrence
            {
                WordId = existingWord.WordId,
                FileId = fileRecord.FileId,
                Count = kvp.Value
            };
            db.Occurrences.Add(occurrence);
        }

        await db.SaveChangesAsync();

        Console.WriteLine($"Indexed file {fileRecord.FileId} with {wordCounts.Count} unique words.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}