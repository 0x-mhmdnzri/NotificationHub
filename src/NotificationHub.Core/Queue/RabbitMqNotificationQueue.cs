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
    public ushort PrefetchCount { get; set; } = 10;
}

public sealed class RabbitMqNotificationQueue : INotificationQueue, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqNotificationQueue> _logger;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(_options.QueueName, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
        _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, _options.RoutingKey).GetAwaiter().GetResult();
        _channel.BasicQosAsync(0, _options.PrefetchCount, false).GetAwaiter().GetResult();

        _logger.LogInformation("RabbitMQ connected. Queue={Queue}", _options.QueueName);
    }

    public async ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions));
        var props = new BasicProperties
        {
            Persistent = true,
            MessageId = request.Id.ToString(),
            CorrelationId = request.CorrelationId,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);

        _logger.LogDebug("Enqueued notification {Id}", request.Id);
    }

    public async IAsyncEnumerable<NotificationRequest> DequeueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        var channel = System.Threading.Channels.Channel.CreateUnbounded<NotificationRequest>();

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var request = JsonSerializer.Deserialize<NotificationRequest>(json, JsonOptions);
                if (request is not null)
                {
                    await channel.Writer.WriteAsync(request, ct);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                }
                else
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process RabbitMQ message");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true, ct);
            }
        };

        await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.CloseAsync();
        if (_connection is not null)
            await _connection.CloseAsync();
    }
}
