import { api } from './client'

export interface AuthMe {
  user: { id: string; email: string; displayName?: string | null }
  tenant: { id: string; name: string } | null
  membershipId?: string | null
  roles: string[]
  permissions: string[]
}

export interface OrgMembership {
  membershipId: string
  organizationId: string
  name: string
  organizationStatus: string
  membershipStatus: string
  roles: string[]
}

export interface MemberRow {
  membershipId: string
  userId: string
  email: string
  displayName?: string | null
  status: string
  roles: string[]
}

export interface OrganizationDto {
  id: string
  name: string
  slug?: string | null
  type: string
  status: string
}

export interface SessionRow {
  id: string
  organizationId?: string | null
  clientId?: string | null
  ip?: string | null
  userAgent?: string | null
  createdAt: string
  lastSeenAt: string
  expiresAt: string
  isActive: boolean
}

export interface TokenResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  tokenType: string
  organizationId?: string
}

export const identityApi = {
  login: (body: { email: string; password: string; organizationId?: string }) =>
    api.post<TokenResponse>('/api/v1/auth/login', body, { anonymous: true }),

  register: (body: {
    email: string
    password: string
    displayName?: string
    createOrganization?: boolean
    organizationName?: string
  }) => api.post<TokenResponse>('/api/v1/auth/register', body, { anonymous: true }),

  refresh: (refreshToken: string) =>
    api.post<TokenResponse>('/api/v1/auth/refresh', { refreshToken }, { anonymous: true }),

  me: () => api.get<AuthMe>('/api/v1/auth/me'),

  organizations: () => api.get<OrgMembership[]>('/api/v1/auth/organizations'),

  switchOrganization: (organizationId: string) =>
    api.post<TokenResponse & { organizationId: string }>(
      '/api/v1/auth/organizations/switch',
      { organizationId },
    ),

  logout: () => api.post<void>('/api/v1/auth/logout'),

  invite: (body: { email: string; roleName?: string; organizationId?: string }) =>
    api.post<{ id: string }>('/api/v1/auth/invitations', body),

  acceptInvite: (token: string) => api.post<void>('/api/v1/auth/invitations/accept', { token }),

  sessions: () => api.get<SessionRow[]>('/api/v1/auth/sessions'),

  revokeSession: (id: string) => api.delete<void>(`/api/v1/auth/sessions/${id}`),

  revokeAllSessions: () => api.post<void>('/api/v1/auth/sessions/revoke-all'),

  getOrganization: (id: string) => api.get<OrganizationDto>(`/api/v1/organizations/${id}`),

  updateOrganization: (id: string, body: { name?: string; status?: string }) =>
    api.patch<OrganizationDto>(`/api/v1/organizations/${id}`, body),

  listMembers: (orgId: string) => api.get<MemberRow[]>(`/api/v1/organizations/${orgId}/members`),

  assignRole: (orgId: string, membershipId: string, roleName: string) =>
    api.post<void>(`/api/v1/organizations/${orgId}/members/${membershipId}/roles`, { roleName }),

  removeRole: (orgId: string, membershipId: string, roleName: string) =>
    api.delete<void>(
      `/api/v1/organizations/${orgId}/members/${membershipId}/roles/${encodeURIComponent(roleName)}`,
    ),

  setMemberStatus: (orgId: string, membershipId: string, status: string) =>
    api.post<void>(`/api/v1/organizations/${orgId}/members/${membershipId}/status`, { status }),
}
