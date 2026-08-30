export const Perm = {
  OrganizationRead: 'organization.read',
  OrganizationUpdate: 'organization.update',
  MemberRead: 'member.read',
  MemberInvite: 'member.invite',
  MemberRoleAssign: 'member.role.assign',
  MemberSuspend: 'member.suspend',
  AuditRead: 'audit.read',
} as const

export function hasPermission(permissions: string[] | undefined, required: string | string[]) {
  if (!permissions?.length) return false
  const need = Array.isArray(required) ? required : [required]
  return need.every((p) => permissions.includes(p))
}

export function hasAnyPermission(permissions: string[] | undefined, required: string[]) {
  if (!permissions?.length) return false
  return required.some((p) => permissions.includes(p))
}
