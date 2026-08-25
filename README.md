# NotificationHub

Multi-channel notification service built with **Microkernel (Plugin) Architecture**.

## Architecture

- **Core (Microkernel)**: Minimal, stable orchestration, plugin lifecycle, routing, status tracking.
- **Abstractions**: Formal plugin contracts (`IPlugin`, `IChannelPlugin`).
- **Plugins**: Channel/provider implementations (Email/SendGrid, SMS/Twilio, ...).
- **Host**: ASP.NET Core entry point that loads and hosts plugins.
- **Sdk**: (WIP) Tools and templates for building new plugins.

## Current Channels

| Channel | Plugin                  | Status |
|---------|-------------------------|--------|
| Email   | SendGrid (stub)         | Ready  |
| SMS     | Twilio (stub)           | Ready  |

## Quick Start

```bash
dotnet restore
dotnet run --project src/NotificationHub.Host
```

Swagger: `https://localhost:7xxx/swagger`

### Send a notification

```http
POST /api/v1/notifications
Content-Type: application/json

{
  "recipient": "user@example.com",
  "channel": "email",
  "templateKey": "welcome",
  "data": { "name": "Ali" }
}
```

### List loaded plugins

```http
GET /api/v1/plugins
```

## Configuration

```json
{
  "Plugins": {
    "SendGrid": { "ApiKey": "SG.xxx" },
    "Twilio": {
      "AccountSid": "ACxxx",
      "AuthToken": "xxx"
    }
  }
}
```

## Adding a New Plugin

1. Create a new class library under `Plugins/`.
2. Reference `NotificationHub.Abstractions`.
3. Implement `IChannelPlugin`.
4. Register it in Host (or place the DLL in the plugins folder for dynamic loading).

## License

MIT
