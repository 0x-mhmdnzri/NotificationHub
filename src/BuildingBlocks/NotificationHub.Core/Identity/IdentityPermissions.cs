namespace NotificationHub.Core.Identity;

/// <summary>Canonical permission names (resource.action). Seeded at startup.</summary>
public static class IdentityPermissions
{
    public const string NotificationRead = "notification.read";
    public const string NotificationSend = "notification.send";

    public const string TemplateRead = "template.read";
    public const string TemplateWrite = "template.write";
    public const string TemplateDelete = "template.delete";

    public const string CampaignRead = "campaign.read";
    public const string CampaignCreate = "campaign.create";
    public const string CampaignStart = "campaign.start";
    public const string CampaignCancel = "campaign.cancel";

    public const string MemberInvite = "member.invite";
    public const string MemberRoleAssign = "member.role.assign";
    public const string MemberSuspend = "member.suspend";
    public const string MemberRead = "member.read";

    public const string OrganizationRead = "organization.read";
    public const string OrganizationCreate = "organization.create";
    public const string OrganizationUpdate = "organization.update";

    public const string AuditRead = "audit.read";

    public static IReadOnlyList<string> All { get; } =
    [
        NotificationRead, NotificationSend,
        TemplateRead, TemplateWrite, TemplateDelete,
        CampaignRead, CampaignCreate, CampaignStart, CampaignCancel,
        MemberInvite, MemberRoleAssign, MemberSuspend, MemberRead,
        OrganizationRead, OrganizationCreate, OrganizationUpdate,
        AuditRead
    ];
}

public static class IdentityRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string OrganizationOwner = "OrganizationOwner";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string NotificationOperator = "NotificationOperator";
    public const string Viewer = "Viewer";
    public const string Auditor = "Auditor";
}
