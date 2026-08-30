# Sprint 3 wire-up

## Program.cs

```csharp
builder.Services.AddNotificationHubJwtBearer(builder.Configuration);
builder.Services.AddNotificationHubRbac();
```

```csharp
app.MapIdentityAuthEndpoints();
app.MapOrganizationAdminEndpoints();
```

## Endpoints

| Method | Path | Permission |
|--------|------|------------|
| POST | /api/v1/organizations | PlatformAdmin |
| GET | /api/v1/organizations/{id} | organization.read |
| PATCH | /api/v1/organizations/{id} | organization.update |
| GET | /api/v1/organizations/{id}/members | member.read |
| POST | .../members/{mid}/roles | member.role.assign |
| DELETE | .../members/{mid}/roles/{name} | member.role.assign |
| POST | .../members/{mid}/status | member.suspend |

## Guarantees

- Deny-by-default (no permission → 403)
- PlatformAdmin cannot be assigned via org member role API
- Suspended/revoked membership fails `GetActiveMembershipAsync`
- API Key machine path unchanged
