export type UUID = string
export type DateTime = string
export type NotificationPriority = 0 | 1 | 2 | 3

export interface NotificationAttachment { fileName?: string | null; contentType?: string | null; content?: string | null }
export interface NotificationRequest {
  id?: UUID; recipient: string; channel?: string | null; channels?: string[] | null; templateKey: string
  data?: Record<string, unknown> | null; priority?: NotificationPriority; scheduledAt?: DateTime | null
  timeZoneId?: string | null; idempotencyKey?: string | null; collapseKey?: string | null; tenantId?: string | null
  locale?: string | null; correlationId?: string | null; category?: string | null; preferredProvider?: string | null
  allowFallback?: boolean; attachments?: NotificationAttachment[] | null
}
export interface TemplateDefinition {
  id?: UUID; key: string; channel: string; locale: string; subject: string; body: string; htmlBody?: string | null
  version?: number; isActive?: boolean; tenantId?: string | null; createdAt?: DateTime
}
export type TemplateListItem = TemplateDefinition
export interface AddRecipientsRequest { addresses: string[]; channels?: string[] | null }
export interface BroadcastRequest {
  name: string; templateKey: string; channel?: string | null; channels?: string[] | null; recipients?: string[] | null
  data?: Record<string, string> | null; tenantId?: string | null; segmentKey?: string | null; locale?: string | null
}
export interface ConsentRecord {
  id?: UUID; subjectId: string; tenantId?: string | null; purpose: string; channel?: string | null; granted?: boolean
  source?: string | null; actor?: string | null; evidence?: string | null; occurredAt?: DateTime
}
export interface CreateCampaignRequest {
  name: string; templateKey: string; channels: string[]; data?: Record<string, string> | null
  scheduledAtUtc?: DateTime | null; tenantId?: string | null
}
export interface EngagementEvent {
  id?: UUID; notificationId?: UUID | null; tenantId?: string | null; eventType: string; recipient?: string | null
  channel?: string | null; url?: string | null; userAgent?: string | null; ipAddress?: string | null
  providerId?: string | null; metadataJson?: string | null; occurredAt?: DateTime
}
export interface RegisterDeviceRequest { userId: string; tenantId?: string | null; platform: string; token: string; locale?: string | null }
export interface SegmentRule { field: string; operator: string; value: string }
export interface SegmentDefinition { id?: UUID; key: string; tenantId?: string | null; rules?: SegmentRule[] | null; matchAll?: boolean }
export interface TopicDefinition { id?: UUID; key: string; name?: string | null; tenantId?: string | null; isActive?: boolean }
export interface UserPreference {
  userId: string; tenantId?: string | null; channelOptIn?: Record<string, boolean> | null; categoryOptIn?: Record<string, boolean> | null
  preferredChannel?: string | null; quietHoursStart?: string | null; quietHoursEnd?: string | null; timeZoneId?: string | null
  maxPerDay?: number | null; weeklySchedule?: Record<string, string> | null; updatedAt?: DateTime
}
export interface WebhookSubscription { id?: UUID; url: string; secret?: string | null; events?: string[] | null; tenantId?: string | null; isActive?: boolean }
export interface WorkflowStep {
  id: string; type: string; channel?: string | null; templateKey?: string | null; preferredProvider?: string | null
  delaySeconds?: number | null; conditionExpression?: string | null; nextOnTrue?: string | null; nextOnFalse?: string | null
  next?: string | null; configJson?: string | null
}
export interface WorkflowDefinition { id?: UUID; key: string; tenantId?: string | null; isActive?: boolean; steps?: WorkflowStep[] | null; createdAt?: DateTime }
export interface WorkflowStartRequest {
  workflowKey: string; recipient: string; tenantId?: string | null; locale?: string | null
  data?: Record<string, unknown> | null; correlationId?: string | null
}
export interface NotificationStatus { id?: UUID; status?: string; [key: string]: unknown }

export type SegmentMatchPayload = Record<string, unknown>
export type ApiList<T> = T[]
