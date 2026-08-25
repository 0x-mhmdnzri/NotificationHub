using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationHub.Core.Queue;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string QueueName { get; set; } = "notifications";
    public string ExchangeName { get; set; } = "notifications.exchange";
    public string RoutingKey { get; set; } = "notification.send";
    public string DeadLetterExchange { get; set; } = "notifications.dlx";
    public string DeadLetterQueue { get; set; } = "notifications.dlq";
    public string DeadLetterRoutingKey { get; set; } = "notification.dead";
    public ushort PrefetchCount { get; set; } = 10;
    public int MaxRedeliveryCount { get; set; } = 5;
    /// <summary>When true, channel awaits broker publisher confirms on publish.</summary>
    public bool PublisherConfirms { get; set; } = true;
    public int PublisherConfirmTimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// AMQP transport: durable exchange/queue, DLX, manual ack after processing.
/// Publish path is also used by Outbox relay.
/// </summary>
public sealed class RabbitMqNotificationQueue : INotificationQueue, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqNotificationQueue> _logger;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public const string HeaderRedeliveryCount = "x-redelivery-count";

    public RabbitMqNotificationQueue(IOptions<RabbitMqOptions> options, ILogger<RabbitMqNotificationQueue> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        var channelOpts = new CreateChannelOptions(
            publisherConfirmationsEnabled: _options.PublisherConfirms,
            publisherConfirmationTrackingEnabled: _options.PublisherConfirms);
        _channel = _connection.CreateChannelAsync(channelOpts).GetAwaiter().GetResult();

        // DLX topology
        _channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
        _channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, _options.DeadLetterRoutingKey).GetAwaiter().GetResult();

        var args = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
        };

        _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(_options.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: args).GetAwaiter().GetResult();
        _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, _options.RoutingKey).GetAwaiter().GetResult();
        _channel.BasicQosAsync(0, _options.PrefetchCount, false).GetAwaiter().GetResult();

        _logger.LogInformation("RabbitMQ connected. Queue={Queue} DLQ={Dlq}", _options.QueueName, _options.DeadLetterQueue);
    }

    public async ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
    {
        // Direct publish (used by outbox relay). Prefer outbox for API path.
        await PublishAsync(request, redeliveryCount: 0, ct);
    }

    public async Task PublishAsync(NotificationRequest request, int redeliveryCount, CancellationToken ct = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        var props = new BasicProperties
        {
            Persistent = true,
            MessageId = request.Id.ToString(),
            CorrelationId = request.CorrelationId,
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                [HeaderRedeliveryCount] = redeliveryCount
            }
        };

        using var confirmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_options.PublisherConfirms)
            confirmCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.PublisherConfirmTimeoutSeconds)));

        try
        {
            // With publisherConfirmationTrackingEnabled, awaiting BasicPublishAsync waits for broker confirm.
            await _channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: _options.RoutingKey,
                mandatory: true,
                basicProperties: props,
                body: body,
                cancellationToken: confirmCts.Token);

            _logger.LogDebug("Published+confirmed notification {Id} redelivery={Count}", request.Id, redeliveryCount);
        }
        catch (OperationCanceledException) when (_options.PublisherConfirms && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Publisher confirm timeout after {_options.PublisherConfirmTimeoutSeconds}s for notification {request.Id}");
        }
    }

    public async IAsyncEnumerable<(NotificationRequest Request, ulong DeliveryTag, int RedeliveryCount)> DequeueWithAckAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        var channel = System.Threading.Channels.Channel.CreateUnbounded<(NotificationRequest, ulong, int)>();

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var request = JsonSerializer.Deserialize<NotificationRequest>(json, JsonOptions);
                if (request is null)
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, ct); // to DLQ (requeue=false)
                    return;
                }

                var redelivery = 0;
                if (ea.BasicProperties.Headers is not null &&
                    ea.BasicProperties.Headers.TryGetValue(HeaderRedeliveryCount, out var raw) &&
                    raw is not null)
                {
                    redelivery = Convert.ToInt32(raw);
                }
                else if (ea.Redelivered)
                {
                    redelivery = 1;
                }

                await channel.Writer.WriteAsync((request, ea.DeliveryTag, redelivery), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize RabbitMQ message");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false, ct);
            }
        };

        await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
            yield return item;
    }

    // Legacy interface: not used by worker anymore when Rabbit is active
    public async IAsyncEnumerable<NotificationRequest> DequeueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (request, deliveryTag, _) in DequeueWithAckAsync(ct))
        {
            await _channel.BasicAckAsync(deliveryTag, false, ct);
            yield return request;
        }
    }

    
    public async Task<(uint WorkQueue, uint DeadLetterQueue)> GetQueueDepthsAsync(CancellationToken ct = default)
    {
        var work = await _channel.QueueDeclarePassiveAsync(_options.QueueName, ct);
        var dlq = await _channel.QueueDeclarePassiveAsync(_options.DeadLetterQueue, ct);
        return (work.MessageCount, dlq.MessageCount);
    }

    public Task AckAsync(ulong deliveryTag, CancellationToken ct = default)
        => _channel.BasicAckAsync(deliveryTag, false, ct).AsTask();

    public Task NackAsync(ulong deliveryTag, bool requeue, CancellationToken ct = default)
        => _channel.BasicNackAsync(deliveryTag, false, requeue, ct).AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}
