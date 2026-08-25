# Security hardening P1 (2026-08-25)

## SEC-07 Click redirect
`RedirectUrlValidator` on `/t/c/{id}`: only http(s), no loopback, no private IP hosts, no userinfo, max 2048 chars.

## SEC-08 Tracking rate limit
`RateLimiting:TrackingPerMinute` (default 120) applied as `track:ip:{ip}:{notificationId}` on `/t/o` and `/t/c`.

## SEC-09 Webhook HMAC v2
Headers: `X-Timestamp`, `X-Nonce`, `X-Signature`, `X-Signature-Version: v2`  
Signature material: `{timestamp}.{nonce}.{body}` HMAC-SHA256 hex with subscription secret.  
Receivers should reject `|now - timestamp| > 300s` and replayed nonces.

## SEC-10 API key hashing
- New keys: `nh_{guid32}_{secret}` + PBKDF2-SHA256 (100k) salted hash `v2.pbkdf2...`
- Validate: parse id → load row → Verify
- Legacy: plain random keys with SHA256 hex still work via `FindByHash`

## SEC-11 Swagger
Enabled only when `IsDevelopment()` (unchanged, documented).

## SEC-12 Input validation
`RequestValidators` for NotificationRequest, TemplateDefinition, WebhookSubscription on write endpoints.

## SEC-13 Expressions
Max length 512, max tokens 128, max paren depth 16, string literal max 256, identifier max 64. Still no code execution.
