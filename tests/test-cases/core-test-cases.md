# NotificationHub Core Test Cases

Feature: Core orchestration, templates, preferences, status, rate limiting  
Version: Phase 1+2  
Date: 2026-08-25

## Coverage Matrix

| Requirement | Test IDs | Priority |
|-------------|----------|----------|
| Template variable rendering | TC-F-001, TC-F-002, TC-E-001, TC-E-002, TC-F-003 | High |
| User preferences / opt-out | TC-F-010..013, TC-ST-010 | High |
| Status persistence + transitions | TC-F-020, TC-F-021, TC-ST-001, TC-E-020 | High |
| Rate limiting | TC-F-030, TC-F-031, TC-E-030 | Medium |
| Accept / idempotency / schedule / suppress | TC-F-040, TC-F-041, TC-ST-010, TC-ST-011 | High |
| Provider send + failover | TC-F-042, TC-F-043, TC-ERR-040 | High |

## Functional

### TC-F-001 Render welcome email variables
- **Requirement:** Template engine replaces `{{name}}`
- **Priority:** High
- **Expected:** Subject/body contain substituted value

### TC-F-040 Accept queues notification
- **Requirement:** Unified accept path stores Queued status
- **Priority:** High
- **Expected:** Status = Queued

### TC-F-041 Idempotent accept
- **Requirement:** Same idempotency key returns existing notification
- **Priority:** High
- **Expected:** Same NotificationId

### TC-F-043 Provider failover
- **Requirement:** When preferred provider fails and AllowFallback=true, next provider is used
- **Priority:** High
- **Expected:** Success via secondary provider

## Edge

### TC-E-001 Unknown template fallback
- **Expected:** Raw template key used as subject/body

### TC-E-030 Rate limit exceeded
- **Expected:** Requests beyond per-minute limit are denied

## Error

### TC-ERR-040 No plugin for channel
- **Expected:** DeliveryResult.Success=false, ErrorCode=NO_PLUGIN

## State Transitions

| Current | Action | Next | Test |
|---------|--------|------|------|
| (none) | Accept | Queued | TC-F-040 |
| (none) | Accept + ScheduledAt future | Scheduled | TC-ST-011 |
| (none) | Accept + channel opt-out | Suppressed | TC-ST-010 |
| Queued | Process success | Sent | TC-F-042 |
| Queued | UpdateStatus Sent | Sent | TC-ST-001 |
