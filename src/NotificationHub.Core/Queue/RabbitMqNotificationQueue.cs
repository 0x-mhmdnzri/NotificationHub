using System.Linq;
using System.Threading;
using System.Threading.Channels;
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

    /// <summary>Legacy single queue (used when ChannelRouting is false).</summary>
    public string QueueName { get; set; } = "notifications";
    public string ExchangeName { get; set; } = "notifications.exchange";
    public string RoutingKey { get; set; } = "notification.send";
    public string DeadLetterExchange { get; set; } = "notifications.dlx";
    public string DeadLetterQueue { get; set; } = "notifications.dlq";
    public string DeadLetterRoutingKey { get; set; } = "notification.dead";
    public string RetryExchangeName { get; set; } = "notifications.retry";

    /// <summary>
    /// When true, declare and route to per-channel work queues (email/sms/push/...).
    /// Aspire channel workers set <see cref="ConsumeChannel"/> to one of these.
    /// </summary>
    public bool ChannelRouting { get; set; } = true;

    /// <summary>Channels that get dedicated queues when ChannelRouting is enabled.</summary>
    public string[] Channels { get; set; } = ["email", "sms", "push", "inapp", "chat"];

    /// <summary>
    /// When set (e.g. "email"), this process only consumes that channel's queue.
    /// Null = consume legacy/default queue only (publisher still routes if ChannelRouting).
    /// </summary>
    public string? ConsumeChannel { get; set; }

    public int[] RetryDelaySeconds { get; set; } = [5, 15, 30, 60, 120];
    public ushort PrefetchCount { get; set; } = 32;

    /// <summary>
    /// Application-level concurrent processors (SemaphoreSlim). Distinct from PrefetchCount (RabbitMQ QoS).
    /// I/O-bound notification delivery: default 8. Keep ≤ PrefetchCount so in-flight work stays in RabbitMQ when saturated.
    /// </summary>
    public int WorkerMaxConcurrency { get; set; } = 16;

    /// <summary>
    /// When true, route by hash(TenantId) % TenantPartitionCount so ordering is preserved per tenant
    /// while different tenants process in parallel (partitioned competing consumers).
    /// </summary>
    public bool PartitionByTenant { get; set; } = false;
    public int TenantPartitionCount { get; set; } = 8;

    /// <summary>
    /// When true, Critical priority messages route to dedicated queues (*.critical)
    /// so they are not starved by bulk notification traffic.
    /// </summary>
    public bool PriorityRouting { get; set; } = true;

    /// <summary>
    /// Bounded hand-off buffer between consumer callback and worker pool. Default = PrefetchCount * 2.
    /// Prevents moving the queue into process memory (anti-pattern).
    /// </summary>
    public int ConsumerBufferCapacity { get; set; } = 0;
    public int MaxRedeliveryCount { get; set; } = 5;
    public bool PublisherConfirms { get; set; } = true;
    public int PublisherConfirmTimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// AMQP transport with durable work queues (optional per-channel), DLQ, delayed redelivery, publisher confirms.
/// </summary>
public sealed class RabbitMqNotificationQueue : INotificationQueue, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqNotificationQueue> _logger;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private readonly int[] _retryDelays;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public const string HeaderRedeliveryCount = "x-redelivery-count";
    public const string HeaderChannel = "x-channel";

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
            "RabbitMQ connected. ChannelRouting={ChannelRouting} ConsumeChannel={Consume} Prefetch={Prefetch}",
            _options.ChannelRouting, _options.ConsumeChannel ?? "(none)", _options.PrefetchCount);
    }

    public static string NormalizeChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            return "email";
        return channel.Trim().ToLowerInvariant();
    }

    public static string WorkQueueName(RabbitMqOptions o, string channel, int? tenantPartition = null, bool critical = false)
    {
        var baseName = o.ChannelRouting ? $"{o.QueueName}.{NormalizeChannel(channel)}" : o.QueueName;
        if (critical && o.PriorityRouting)
            baseName = $"{baseName}.critical";
        if (o.PartitionByTenant && tenantPartition is int p)
            return $"{baseName}.t{p}";
        return baseName;
    }

    public static string WorkRoutingKey(RabbitMqOptions o, string channel, int? tenantPartition = null, bool critical = false)
    {
        var baseKey = o.ChannelRouting ? $"{o.RoutingKey}.{NormalizeChannel(channel)}" : o.RoutingKey;
        if (critical && o.PriorityRouting)
            baseKey = $"{baseKey}.critical";
        if (o.PartitionByTenant && tenantPartition is int p)
            return $"{baseKey}.t{p}";
        return baseKey;
    }

    public static bool IsCriticalPriority(NotificationRequest request)
        => request.Priority == NotificationPriority.Critical;

    public static int TenantPartitionIndex(RabbitMqOptions o, string? tenantId)
    {
        var n = Math.Max(1, o.TenantPartitionCount);
        if (string.IsNullOrWhiteSpace(tenantId)) return 0;
        // Stable non-cryptographic partition for ordering per tenant.
        var hash = tenantId.GetHashCode(StringComparison.Ordinal);
        return Math.Abs(hash) % n;
    }

    private void DeclareTopology()
    {
        _channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
        _channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, _options.DeadLetterRoutingKey).GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(_options.RetryExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();

        var channels = _options.ChannelRouting
            ? (_options.Channels is { Length: > 0 } ? _options.Channels : ["email", "sms", "push"])
            : [""];

        var partitions = _options.PartitionByTenant
            ? Enumerable.Range(0, Math.Max(1, _options.TenantPartitionCount)).Cast<int?>().ToArray()
            : new int?[] { null };

        var criticalFlags = _options.PriorityRouting ? new[] { false, true } : new[] { false };

        foreach (var ch in channels)
        {
          foreach (var critical in criticalFlags)
          {
            foreach (var part in partitions)
            {
                var channelKey = string.IsNullOrEmpty(ch) ? null : NormalizeChannel(ch);
                string qName, rk;
                if (channelKey is null)
                {
                    qName = _options.QueueName;
                    rk = _options.RoutingKey;
                    if (critical) { qName += ".critical"; rk += ".critical"; }
                    if (part is int tp) { qName += $".t{tp}"; rk += $".t{tp}"; }
                }
                else
                {
                    qName = WorkQueueName(_options, channelKey, part, critical);
                    rk = WorkRoutingKey(_options, channelKey, part, critical);
                }

                var workArgs = new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
                };
                _channel.QueueDeclareAsync(qName, durable: true, exclusive: false, autoDelete: false, arguments: workArgs)
                    .GetAwaiter().GetResult();
                _channel.QueueBindAsync(qName, _options.ExchangeName, rk).GetAwaiter().GetResult();

                foreach (var delay in _retryDelays)
                {
                    var rq = RetryQueueName(qName, delay);
                    var rrk = RetryRoutingKey(channelKey, delay);
                    var args = new Dictionary<string, object?>
                    {
                        ["x-message-ttl"] = delay * 1000,
                        ["x-dead-letter-exchange"] = _options.ExchangeName,
                        ["x-dead-letter-routing-key"] = rk
                    };
                    _channel.QueueDeclareAsync(rq, durable: true, exclusive: false, autoDelete: false, arguments: args)
                        .GetAwaiter().GetResult();
                    _channel.QueueBindAsync(rq, _options.RetryExchangeName, rrk).GetAwaiter().GetResult();
                }
            }
          }
        }

        _channel.BasicQosAsync(0, _options.PrefetchCount, false).GetAwaiter().GetResult();
    }

    private static string RetryQueueName(string workQueue, int delaySeconds) => $"{workQueue}.retry.{delaySeconds}s";

    private static string RetryRoutingKey(string? channel, int delaySeconds)
        => channel is null
            ? $"notification.retry.{delaySeconds}"
            : $"notification.retry.{channel}.{delaySeconds}";

    private string ResolveChannel(NotificationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Channel))
            return NormalizeChannel(request.Channel);
        if (request.Channels is { Length: > 0 } && !string.IsNullOrWhiteSpace(request.Channels[0]))
            return NormalizeChannel(request.Channels[0]);
        return "email";
    }

    public async ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
        => await PublishAsync(request, redeliveryCount: 0, ct);

    public async Task PublishAsync(NotificationRequest request, int redeliveryCount, CancellationToken ct = default)
    {
        var channel = ResolveChannel(request);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        var props = BuildProps(request, redeliveryCount, channel);
        int? part = _options.PartitionByTenant
            ? TenantPartitionIndex(_options, request.TenantId)
            : null;
        var critical = _options.PriorityRouting && IsCriticalPriority(request);
        var rk = WorkRoutingKey(_options, channel, part, critical);
        if (!_options.ChannelRouting)
        {
            rk = _options.RoutingKey;
            if (critical) rk += ".critical";
            if (part is int tp) rk += $".t{tp}";
        }
        await PublishWithConfirmAsync(_options.ExchangeName, rk, props, body, ct);
        _logger.LogDebug("Published+confirmed notification {Id} channel={Channel} redelivery={Count}",
            request.Id, channel, redeliveryCount);
    }

    public async Task ScheduleDelayedRedeliveryAsync(NotificationRequest request, int currentRedeliveryCount, CancellationToken ct = default)
    {
        var next = currentRedeliveryCount + 1;
        if (next > _options.MaxRedeliveryCount)
            throw new InvalidOperationException($"Max redelivery exceeded for {request.Id}");

        var channel = ResolveChannel(request);
        var delay = _retryDelays[Math.Min(next - 1, _retryDelays.Length - 1)];
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        var props = BuildProps(request, next, channel);
        var rk = RetryRoutingKey(_options.ChannelRouting ? channel : null, delay);

        await PublishWithConfirmAsync(_options.RetryExchangeName, rk, props, body, ct);
        _logger.LogInformation(
            "Scheduled delayed redelivery for {Id} channel={Channel} attempt={Attempt} delay={Delay}s",
            request.Id, channel, next, delay);
    }

    private BasicProperties BuildProps(NotificationRequest request, int redeliveryCount, string channel) => new()
    {
        Persistent = true,
        MessageId = request.Id.ToString(),
        CorrelationId = request.CorrelationId,
        ContentType = "application/json",
        Headers = new Dictionary<string, object?>
        {
            [HeaderRedeliveryCount] = redeliveryCount,
            [HeaderChannel] = channel
        }
    };

    private async Task PublishWithConfirmAsync(string exchange, string routingKey, BasicProperties props, byte[] body, CancellationToken ct)
    {
        // IChannel is not multi-thread safe — serialize concurrent publishers (outbox parallel path).
        await _publishGate.WaitAsync(ct);
        try
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
        finally
        {
            _publishGate.Release();
        }
    }

    private IReadOnlyList<string> ResolveConsumeQueues()
    {
        var queues = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.ConsumeChannel))
        {
            queues.Add(WorkQueueName(_options, _options.ConsumeChannel, critical: false));
            if (_options.PriorityRouting)
                queues.Add(WorkQueueName(_options, _options.ConsumeChannel, critical: true));
        }
        else
        {
            queues.Add(_options.QueueName);
            if (_options.PriorityRouting)
                queues.Add($"{_options.QueueName}.critical");
        }
        return queues;
    }

    public async IAsyncEnumerable<(NotificationRequest Request, ulong DeliveryTag, int RedeliveryCount)> DequeueWithAckAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var queueNames = ResolveConsumeQueues();
        var consumer = new AsyncEventingBasicConsumer(_channel);
        // Bounded buffer: backpressure when workers are saturated (does not drain RabbitMQ into RAM).
        var bufferCap = _options.ConsumerBufferCapacity > 0
            ? _options.ConsumerBufferCapacity
            : Math.Max(2, _options.PrefetchCount * 2);
        var channel = System.Threading.Channels.Channel.CreateBounded<(NotificationRequest, ulong, int)>(
            new BoundedChannelOptions(bufferCap)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

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

        foreach (var queueName in queueNames)
        {
            await _channel.BasicConsumeAsync(queueName, autoAck: false, consumer, ct);
            _logger.LogInformation(
                "Consuming RabbitMQ queue {Queue} prefetch={Prefetch} buffer={Buffer} maxConcurrency={Concurrency}",
                queueName, _options.PrefetchCount, bufferCap, _options.WorkerMaxConcurrency);
        }

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
        uint work = 0;
        if (_options.ChannelRouting)
        {
            foreach (var ch in _options.Channels)
            {
                var q = await _channel.QueueDeclarePassiveAsync(WorkQueueName(_options, ch), ct);
                work += q.MessageCount;
            }
        }
        else
        {
            var q = await _channel.QueueDeclarePassiveAsync(_options.QueueName, ct);
            work = q.MessageCount;
        }

        var dlq = await _channel.QueueDeclarePassiveAsync(_options.DeadLetterQueue, ct);
        uint retry = 0;
        var bases = _options.ChannelRouting
            ? _options.Channels.Select(c => WorkQueueName(_options, c))
            : [_options.QueueName];
        foreach (var baseQ in bases)
        {
            foreach (var delay in _retryDelays)
            {
                var q = await _channel.QueueDeclarePassiveAsync(RetryQueueName(baseQ, delay), ct);
                retry += q.MessageCount;
            }
        }
        return (work, dlq.MessageCount, retry);
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
