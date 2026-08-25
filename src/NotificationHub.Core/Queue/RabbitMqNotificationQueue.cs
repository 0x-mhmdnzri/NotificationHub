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
    public string RetryExchangeName { get; set; } = "notifications.retry";
    /// <summary>Backoff steps in seconds for delayed redelivery (TTL queues).</summary>
    public int[] RetryDelaySeconds { get; set; } = [5, 15, 30, 60, 120];
    public ushort PrefetchCount { get; set; } = 10;
    public int MaxRedeliveryCount { get; set; } = 5;
    public bool PublisherConfirms { get; set; } = true;
    public int PublisherConfirmTimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// AMQP transport with:
/// - durable work queue + DLQ
/// - delayed redelivery via per-delay TTL queues that dead-letter back to the work queue
/// - publisher confirms
/// - manual ack after processing
/// </summary>
public sealed class RabbitMqNotificationQueue : INotificationQueue, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqNotificationQueue> _logger;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly int[] _retryDelays;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public const string HeaderRedeliveryCount = "x-redelivery-count";

    public RabbitMqNotificationQueue(IOptions<RabbitMqOptions> options, ILogger<RabbitMqNotificationQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
        _retryDelays = (_options.RetryDelaySeconds is { Length: > 0 }
            ? _options.RetryDelaySeconds
            : [5, 15, 30, 60, 120]).OrderBy(x => x).ToArray();

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

        DeclareTopology();
        _logger.LogInformation(
            "RabbitMQ connected. Queue={Queue} DLQ={Dlq} Prefetch={Prefetch} Confirms={Confirms} RetryDelays=[{Delays}]",
            _options.QueueName, _options.DeadLetterQueue, _options.PrefetchCount, _options.PublisherConfirms,
            string.Join(',', _retryDelays));
    }

    private void DeclareTopology()
    {
        // Final DLQ for poison messages
        _channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
        _channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, _options.DeadLetterRoutingKey).GetAwaiter().GetResult();

        // Main work exchange/queue (failed permanent → DLQ)
        var workArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
        };
        _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(_options.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: workArgs).GetAwaiter().GetResult();
        _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, _options.RoutingKey).GetAwaiter().GetResult();
        _channel.BasicQosAsync(0, _options.PrefetchCount, false).GetAwaiter().GetResult();

        // Retry exchange + per-delay TTL queues that dead-letter back to the work queue
        _channel.ExchangeDeclareAsync(_options.RetryExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        foreach (var delay in _retryDelays)
        {
            var q = RetryQueueName(delay);
            var rk = RetryRoutingKey(delay);
            var args = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = delay * 1000,
                ["x-dead-letter-exchange"] = _options.ExchangeName,
                ["x-dead-letter-routing-key"] = _options.RoutingKey
            };
            _channel.QueueDeclareAsync(q, durable: true, exclusive: false, autoDelete: false, arguments: args).GetAwaiter().GetResult();
            _channel.QueueBindAsync(q, _options.RetryExchangeName, rk).GetAwaiter().GetResult();
        }
    }

    private string RetryQueueName(int delaySeconds) => $"{_options.QueueName}.retry.{delaySeconds}s";
    private static string RetryRoutingKey(int delaySeconds) => $"notification.retry.{delaySeconds}";

    public async ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
        => await PublishAsync(request, redeliveryCount: 0, ct);

    public async Task PublishAsync(NotificationRequest request, int redeliveryCount, CancellationToken ct = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        var props = BuildProps(request, redeliveryCount);
        await PublishWithConfirmAsync(_options.ExchangeName, _options.RoutingKey, props, body, ct);
        _logger.LogDebug("Published+confirmed notification {Id} redelivery={Count}", request.Id, redeliveryCount);
    }

    /// <summary>
    /// Schedules delayed redelivery via TTL retry queue. Does not requeue the original delivery.
    /// Caller must ack the original message after this succeeds.
    /// </summary>
    public async Task ScheduleDelayedRedeliveryAsync(NotificationRequest request, int currentRedeliveryCount, CancellationToken ct = default)
    {
        var next = currentRedeliveryCount + 1;
        if (next > _options.MaxRedeliveryCount)
            throw new InvalidOperationException($"Max redelivery exceeded for {request.Id}");

        var delay = _retryDelays[Math.Min(next - 1, _retryDelays.Length - 1)];
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        var props = BuildProps(request, next);
        var rk = RetryRoutingKey(delay);

        await PublishWithConfirmAsync(_options.RetryExchangeName, rk, props, body, ct);
        _logger.LogInformation(
            "Scheduled delayed redelivery for {Id} attempt={Attempt} delay={Delay}s",
            request.Id, next, delay);
    }

    private BasicProperties BuildProps(NotificationRequest request, int redeliveryCount) => new()
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

    private async Task PublishWithConfirmAsync(string exchange, string routingKey, BasicProperties props, byte[] body, CancellationToken ct)
    {
        using var confirmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_options.PublisherConfirms)
            confirmCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.PublisherConfirmTimeoutSeconds)));

        try
        {
            await _channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: props,
                body: body,
                cancellationToken: confirmCts.Token);
        }
        catch (OperationCanceledException) when (_options.PublisherConfirms && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Publisher confirm timeout after {_options.PublisherConfirmTimeoutSeconds}s");
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
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, ct);
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

    public async IAsyncEnumerable<NotificationRequest> DequeueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (request, deliveryTag, _) in DequeueWithAckAsync(ct))
        {
            await _channel.BasicAckAsync(deliveryTag, false, ct);
            yield return request;
        }
    }

    public async Task<(uint WorkQueue, uint DeadLetterQueue, uint RetryQueue)> GetQueueDepthsAsync(CancellationToken ct = default)
    {
        var work = await _channel.QueueDeclarePassiveAsync(_options.QueueName, ct);
        var dlq = await _channel.QueueDeclarePassiveAsync(_options.DeadLetterQueue, ct);
        uint retry = 0;
        foreach (var delay in _retryDelays)
        {
            var q = await _channel.QueueDeclarePassiveAsync(RetryQueueName(delay), ct);
            retry += q.MessageCount;
        }
        return (work.MessageCount, dlq.MessageCount, retry);
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
