using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Indexer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;

public class IndexWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexWorker> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly ActivitySource ActivitySource = new("Indexer");
    private static readonly Meter Meter = new("IndexerMeter");
    private static readonly Counter<long> EmailsProcessed = Meter.CreateCounter<long>("emails_processed", "Number of emails processed by the Indexer");

    public IndexWorker(IServiceProvider serviceProvider, ILogger<IndexWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var port = Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672";
        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = int.Parse(port),
            UserName = "guest",
            Password = "guest"
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

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
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            using var activity = ActivitySource.StartActivity("RabbitMQ Consume", ActivityKind.Consumer);

            try
            {
                activity?.SetTag("queue", "cleaned_emails");

                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var cleanedEmail = JsonSerializer.Deserialize<CleanedEmail>(message);

                if (cleanedEmail != null)
                {
                    await IndexEmailAsync(cleanedEmail);

                    EmailsProcessed.Add(1);
                    _logger.LogInformation("Successfully processed email: {FileName}", cleanedEmail.FileName);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Error processing message");
            }
        };

        _channel.BasicConsumeAsync(queue: "cleaned_emails", autoAck: false, consumer: consumer);

        _logger.LogInformation("Indexer is now listening for messages on 'cleaned_emails'...");
        return Task.CompletedTask;
    }

    private async Task IndexEmailAsync(CleanedEmail cleanedEmail)
    {
        using var activity = ActivitySource.StartActivity("IndexEmail", ActivityKind.Internal);
        activity?.SetTag("email", cleanedEmail.FileName);

        try
        {
            using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IndexerContext>();

            var fileRecord = new FileRecord
            {
                FileName = cleanedEmail.FileName,
                Content = cleanedEmail.Data,
            };
            db.Files.Add(fileRecord);
            await db.SaveChangesAsync();

            var tokens = cleanedEmail.Content.Split(new[] { ' ', '\r', '\n', '\t', ',', '.', ';', ':', '!', '?', '\"', '\'' }, StringSplitOptions.RemoveEmptyEntries);

            var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens)
            {
                var wordStr = token.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(wordStr)) continue;

                if (!wordCounts.ContainsKey(wordStr))
                    wordCounts[wordStr] = 0;
                wordCounts[wordStr]++;
            }

            foreach (var kvp in wordCounts)
            {
                var existingWord = await db.Words.FirstOrDefaultAsync(w => w.Word == kvp.Key);
                if (existingWord == null)
                {
                    existingWord = new Words { Word = kvp.Key };
                    db.Words.Add(existingWord);
                    await db.SaveChangesAsync();
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
            _logger.LogInformation("Indexed file {FileId} with {WordCount} unique words.", fileRecord.FileId, wordCounts.Count);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
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
