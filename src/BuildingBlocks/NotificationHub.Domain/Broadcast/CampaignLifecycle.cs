using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Broadcast;

/// <summary>
/// Explicit lifecycle state machine for broadcast campaigns.
/// Delivery counts are inputs to completion resolution — not stored inside the aggregate as recipient rows
/// (large fan-out stays outside the consistency boundary).
/// </summary>
public static class CampaignLifecycle
{
    private static readonly Dictionary<CampaignStatus, HashSet<CampaignStatus>> Allowed = new()
    {
        [CampaignStatus.Draft] = [CampaignStatus.Scheduled, CampaignStatus.Preparing, CampaignStatus.Processing, CampaignStatus.Cancelled],
        [CampaignStatus.Scheduled] = [CampaignStatus.Preparing, CampaignStatus.Processing, CampaignStatus.Cancelled],
        [CampaignStatus.Preparing] = [CampaignStatus.Dispatching, CampaignStatus.Processing, CampaignStatus.Failed, CampaignStatus.Cancelled],
        [CampaignStatus.Dispatching] = [CampaignStatus.Delivering, CampaignStatus.Processing, CampaignStatus.Failed, CampaignStatus.Cancelled],
        [CampaignStatus.Delivering] = [CampaignStatus.Completed, CampaignStatus.PartiallyCompleted, CampaignStatus.Failed, CampaignStatus.Cancelled],
        [CampaignStatus.Processing] = [CampaignStatus.Delivering, CampaignStatus.Completed, CampaignStatus.PartiallyCompleted, CampaignStatus.Failed, CampaignStatus.Cancelled],
        [CampaignStatus.Completed] = [],
        [CampaignStatus.PartiallyCompleted] = [],
        [CampaignStatus.Failed] = [CampaignStatus.Preparing], // operational recovery
        [CampaignStatus.Cancelled] = []
    };

    public static bool CanTransition(CampaignStatus from, CampaignStatus to)
        => from == to || (Allowed.TryGetValue(from, out var set) && set.Contains(to));

    public static void Ensure(CampaignStatus from, CampaignStatus to)
    {
        if (!CanTransition(from, to))
            throw new DomainException($"Illegal campaign transition {from} → {to}");
    }

    public static CampaignStatus ResolveCompletion(long total, long sent, long failed, long cancelled, long skipped)
    {
        var terminal = sent + failed + cancelled + skipped;
        if (total > 0 && terminal < total)
            return CampaignStatus.Delivering;
        if (failed > 0 && sent > 0)
            return CampaignStatus.PartiallyCompleted;
        if (failed > 0 && sent == 0)
            return CampaignStatus.Failed;
        return CampaignStatus.Completed;
    }

    public static bool IsTerminal(CampaignStatus status)
        => status is CampaignStatus.Completed or CampaignStatus.PartiallyCompleted
            or CampaignStatus.Failed or CampaignStatus.Cancelled;

    public static bool IsActive(CampaignStatus status)
        => status is CampaignStatus.Preparing or CampaignStatus.Dispatching
            or CampaignStatus.Delivering or CampaignStatus.Processing or CampaignStatus.Scheduled;
}
