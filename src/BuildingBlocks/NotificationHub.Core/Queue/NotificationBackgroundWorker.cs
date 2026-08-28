using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Store;

namespace NotificationHub.Core.Queue;

/// <summary>
/// Production worker pool for notification delivery.
/// Algorithm: Competing Consumers + Fair Dispatch (RabbitMQ QoS prefetch) + application-level
/// bounded concurrency (SemaphoreSlim). Prefetch and SemaphoreSlim control different layers —
/// do not treat them as interchangeable.
/// Workload: I/O-bound (provider HTTP), variable duration → moderate prefetch + parallel workers.
/// Semantics: at-least-once; ACK only after successful process + inbox mark; delayed retry + DLQ.
/// </summary>
public sealed class NotificationBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _root;
    private readonly ILogger<NotificationBackgroundWorker> _logger;

    public NotificationBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        IServiceProvider root,
        ILogger<NotificationBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _root = root;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background worker started");

        var rabbit = _root.GetService<RabbitMqNotificationQueue>();
        if (rabbit is not null)
        {
            // Retry on AMQP topology/connection faults — do not stop the host (StopHost policy).
            var delay = TimeSpan.FromSeconds(2);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunRabbitWorkerPoolAsync(rabbit, stoppingToken);
                    break; // normal exit (cancellation / end of stream)
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "RabbitMQ worker pool failed; retrying in {Delay}s (host stays up)",
                        delay.TotalSeconds);
                    try { await Task.Delay(delay, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                    delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
                }
            }
            return;
        }

        await RunInMemoryFallbackAsync(stoppingToken);
    }

    /// <summary>
    /// Fair dispatch + competing application workers.
    /// RabbitMQ delivers up to PrefetchCount unacked messages; SemaphoreSlim limits concurrent ProcessOne.
    /// ACK/NACK on a single channel is serialized (RabbitMQ.Client channel is not multi-thread safe).
    /// </summary>
    private async Task RunRabbitWorkerPoolAsync(RabbitMqNotificationQueue rabbit, CancellationToken stoppingToken)
    {
        var options = _root.GetService<IOptions<RabbitMqOptions>>()?.Value ?? new RabbitMqOptions();
        var maxRedelivery = options.MaxRedeliveryCount;
        var concurrency = Math.Max(1, options.WorkerMaxConcurrency);

        _logger.LogInformation(
            "Rabbit worker pool: concurrency={Concurrency} prefetch={Prefetch} maxRedelivery={MaxRedelivery}",
            concurrency, options.PrefetchCount, maxRedelivery);

        using var gate = new SemaphoreSlim(concurrency, concurrency);
        // Serialize ACK/NACK — IChannel is not thread-safe for concurrent basic.ack
        var ackGate = new SemaphoreSlim(1, 1);
        var inFlight = new List<Task>();
        var inFlightLock = new object();

        try
        {
            await foreach (var (request, deliveryTag, redelivery) in rabbit.DequeueWithAckAsync(stoppingToken))
            {
                await gate.WaitAsync(stoppingToken);

                var task = ProcessRabbitItemAsync(
                    rabbit, request, deliveryTag, redelivery, maxRedelivery, gate, ackGate, stoppingToken);

                lock (inFlightLock)
                {
                    inFlight.Add(task);
                    inFlight.RemoveAll(t => t.IsCompleted);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful drain
        }

        Task[] pending;
        lock (inFlightLock)
            pending = inFlight.Where(t => !t.IsCompleted).ToArray();

        if (pending.Length > 0)
        {
            _logger.LogInformation("Draining {Count} in-flight notification workers", pending.Length);
            try { await Task.WhenAll(pending); }
            catch { /* individual tasks already logged */ }
        }

        _logger.LogInformation("Notification background worker stopped");
    }

    private async Task ProcessRabbitItemAsync(
        RabbitMqNotificationQueue rabbit,
        NotificationRequest request,
        ulong deliveryTag,
        int redelivery,
        int maxRedelivery,
        SemaphoreSlim gate,
        SemaphoreSlim ackGate,
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();

            if (await inbox.ExistsAsync(request.Id.ToString(), stoppingToken))
            {
                _logger.LogInformation("Duplicate message {Id} skipped (inbox)", request.Id);
                await AckSafeAsync(rabbit, ackGate, deliveryTag, stoppingToken);
                return;
            }

            await ProcessOneAsync(scope, request, stoppingToken);

            await inbox.TryMarkProcessedAsync(request.Id.ToString(), stoppingToken);
            await AckSafeAsync(rabbit, ackGate, deliveryTag, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process notification {Id} (redelivery={Redelivery})", request.Id, redelivery);
            try
            {
                if (redelivery >= maxRedelivery)
                {
                    await NackSafeAsync(rabbit, ackGate, deliveryTag, requeue: false, stoppingToken);
                }
                else
                {
                    await rabbit.ScheduleDelayedRedeliveryAsync(request, redelivery, stoppingToken);
                    await AckSafeAsync(rabbit, ackGate, deliveryTag, stoppingToken);
                }
            }
            catch (Exception scheduleEx)
            {
                _logger.LogError(scheduleEx, "Failed to schedule delayed redelivery for {Id}", request.Id);
                try
                {
                    await NackSafeAsync(rabbit, ackGate, deliveryTag, requeue: false, stoppingToken);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "Failed to nack {Id}", request.Id);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task AckSafeAsync(
        RabbitMqNotificationQueue rabbit, SemaphoreSlim ackGate, ulong deliveryTag, CancellationToken ct)
    {
        await ackGate.WaitAsync(ct);
        try { await rabbit.AckAsync(deliveryTag, ct); }
        finally { ackGate.Release(); }
    }

    private static async Task NackSafeAsync(
        RabbitMqNotificationQueue rabbit, SemaphoreSlim ackGate, ulong deliveryTag, bool requeue, CancellationToken ct)
    {
        await ackGate.WaitAsync(ct);
        try { await rabbit.NackAsync(deliveryTag, requeue, ct); }
        finally { ackGate.Release(); }
    }

    private async Task RunInMemoryFallbackAsync(CancellationToken stoppingToken)
    {
        using var memScope = _scopeFactory.CreateScope();
        var queue = memScope.ServiceProvider.GetRequiredService<INotificationQueue>();
        await foreach (var request in queue.DequeueAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
                if (!await inbox.TryMarkProcessedAsync(request.Id.ToString(), stoppingToken))
                    continue;
                await ProcessOneAsync(scope, request, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification {Id}", request.Id);
            }
        }
    }

    private static async Task ProcessOneAsync(IServiceScope scope, NotificationRequest request, CancellationToken ct)
    {
        var orchestrator = scope.ServiceProvider.GetRequiredService<NotificationOrchestrator>();
        var statusStore = scope.ServiceProvider.GetRequiredService<INotificationStatusStore>();
        var repo = scope.ServiceProvider.GetService<NotificationHub.Domain.Delivery.INotificationRepository>();
        var uow = scope.ServiceProvider.GetService<NotificationHub.Domain.Common.IUnitOfWork>();

        var now = DateTimeOffset.UtcNow;
        if (repo is not null)
        {
            var n = await repo.GetAsync(NotificationHub.Domain.Delivery.ValueObjects.NotificationId.From(request.Id), ct);
            if (n is not null)
            {
                n.MarkProcessing(now);
                await repo.UpdateAsync(n, ct);
                if (uow is not null) await uow.SaveChangesAsync(ct);
                else await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Processing, attemptCount: n.AttemptCount, ct: ct);
            }
            else
                await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Processing, ct: ct);
        }
        else
            await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Processing, ct: ct);

        var result = await orchestrator.ProcessAsync(request, ct);

        if (repo is not null)
        {
            var n = await repo.GetAsync(NotificationHub.Domain.Delivery.ValueObjects.NotificationId.From(request.Id), ct);
            if (n is not null)
            {
                if (result.Success)
                    n.MarkSent(result.ProviderId ?? "unknown", result.ProviderMessageId, DateTimeOffset.UtcNow);
                else
                    n.MarkFailed(result.ErrorCode, result.ErrorMessage, maxAttempts: 3, DateTimeOffset.UtcNow);
                await repo.UpdateAsync(n, ct);
                if (uow is not null) await uow.SaveChangesAsync(ct);
                else
                {
                    await statusStore.UpdateStatusAsync(request.Id, (DeliveryStatus)(int)n.Status,
                        providerMessageId: result.ProviderMessageId,
                        errorCode: result.ErrorCode, errorMessage: result.ErrorMessage,
                        attemptCount: n.AttemptCount, ct: ct);
                }
                if (!result.Success)
                    throw new InvalidOperationException($"Provider delivery failed: {result.ErrorCode} {result.ErrorMessage}");
                return;
            }
        }

        if (result.Success)
        {
            await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Sent,
                providerMessageId: result.ProviderMessageId,
                attemptCount: result.AttemptNumber,
                ct: ct);
        }
        else
        {
            var finalStatus = result.AttemptNumber >= 3 ? DeliveryStatus.DeadLetter : DeliveryStatus.Failed;
            await statusStore.UpdateStatusAsync(request.Id, finalStatus,
                errorCode: result.ErrorCode,
                errorMessage: result.ErrorMessage,
                attemptCount: result.AttemptNumber,
                ct: ct);

            throw new InvalidOperationException(
                $"Provider delivery failed: {result.ErrorCode} {result.ErrorMessage}");
        }
    }
}
