using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow;

public sealed class WorkflowTimeline : IWorkflowTimeline
{
    private readonly NotificationDbContext _db;
    public WorkflowTimeline(NotificationDbContext db) => _db = db;

    public async Task AppendAsync(Guid runId, string eventType, string? stepId = null, string? message = null, object? data = null, CancellationToken ct = default)
    {
        _db.WorkflowTimelineEvents.Add(new WorkflowTimelineEventEntity
        {
            RunId = runId,
            EventType = eventType,
            StepId = stepId,
            Message = message,
            DataJson = data is null ? null : JsonSerializer.Serialize(data),
            OccurredAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTimelineEventDto>> GetTimelineAsync(Guid runId, CancellationToken ct = default)
    {
        var rows = await _db.WorkflowTimelineEvents.AsNoTracking()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(ct);

        return rows.Select(x => new WorkflowTimelineEventDto
        {
            Id = x.Id,
            RunId = x.RunId,
            EventType = x.EventType,
            StepId = x.StepId,
            Message = x.Message,
            DataJson = x.DataJson,
            OccurredAt = x.OccurredAt
        }).ToList();
    }
}
