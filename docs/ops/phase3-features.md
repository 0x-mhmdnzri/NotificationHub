# Phase 3 (F15–F21) — Channels & Microkernel

| ID | Feature | Plugin / component |
|----|---------|-------------------|
| F15 | Telegram | `chat-telegram` — Bot API sendMessage HTML |
| F16 | Discord + Teams | `chat-discord`, `chat-teams` |
| F17 | Twilio / Resend / SES | `sms-twilio`, `email-resend`, `email-ses` |
| F18 | Expo Push | `push-expo` |
| F19 | Circuit breaker | `CircuitBreakerProviderHealthTracker` |
| F20 | Hot-load | `PluginLoader.ReloadDirectoryAsync` + admin API |
| F21 | Certification | `ChannelPluginCertificationTests` + this doc |

## Config keys
- `Plugins:Telegram:BotToken`
- `Plugins:Discord:WebhookUrl`
- `Plugins:Teams:WebhookUrl`
- `Plugins:Twilio:AccountSid|AuthToken|From`
- `Plugins:Resend:ApiKey|From`
- `Plugins:Ses:AccessKeyId|SecretAccessKey|Region|From`
- `Plugins:Expo:AccessToken`
- `Plugins:Directory` — optional DLL folder for hot-load
- `CircuitBreaker:FailureThreshold|OpenDurationSeconds`
