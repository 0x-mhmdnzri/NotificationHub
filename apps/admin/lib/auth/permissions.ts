export const Perm = {
  OrganizationRead: 'organization.read',
  OrganizationUpdate: 'organization.update',
  MemberRead: 'member.read',
  MemberInvite: 'member.invite',
  MemberRoleAssign: 'member.role.assign',
  MemberSuspend: 'member.suspend',
  AuditRead: 'audit.read',
  NotificationRead: 'notification.read',
  NotificationSend: 'notification.send',
  TemplateRead: 'template.read',
  TemplateWrite: 'template.write',
  CampaignRead: 'campaign.read',
  CampaignCreate: 'campaign.create',
} as const

const PLATFORM_ROLES = new Set(['SuperAdmin', 'PlatformAdmin'])

export function isPlatformAdmin(roles: string[] | undefined): boolean {
  return !!roles?.some((r) => PLATFORM_ROLES.has(r))
}

export function hasPermission(
  permissions: string[] | undefined,
  rolesOrRequired?: string | string[],
  requiredMaybe?: string | string[],
) {
  // Support both (perms, required) and (perms, roles, required)
  let roles: string[] | undefined
  let required: string | string[] | undefined

  if (requiredMaybe !== undefined) {
    roles = rolesOrRequired as string[] | undefined
    required = requiredMaybe
  } else {
    required = rolesOrRequired
  }

  if (isPlatformAdmin(roles)) return true
  if (!permissions?.length || required === undefined) return false
  const need = Array.isArray(required) ? required : [required]
  return need.every((p) => permissions.includes(p))
}

export function hasAnyPermission(
  permissions: string[] | undefined,
  required: string[],
  roles?: string[],
) {
  if (isPlatformAdmin(roles)) return true
  if (!permissions?.length) return false
  return required.some((p) => permissions.includes(p))
}
