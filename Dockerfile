# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NotificationHub.sln ./
COPY src/NotificationHub.Abstractions/NotificationHub.Abstractions.csproj src/NotificationHub.Abstractions/
COPY src/NotificationHub.Core/NotificationHub.Core.csproj src/NotificationHub.Core/
COPY src/NotificationHub.Host/NotificationHub.Host.csproj src/NotificationHub.Host/
COPY src/NotificationHub.Sdk/NotificationHub.Sdk.csproj src/NotificationHub.Sdk/
COPY Plugins/NotificationHub.Plugins.Email.SendGrid/NotificationHub.Plugins.Email.SendGrid.csproj Plugins/NotificationHub.Plugins.Email.SendGrid/
COPY Plugins/NotificationHub.Plugins.Email.Smtp/NotificationHub.Plugins.Email.Smtp.csproj Plugins/NotificationHub.Plugins.Email.Smtp/
COPY Plugins/NotificationHub.Plugins.Sms.Kavenegar/NotificationHub.Plugins.Sms.Kavenegar.csproj Plugins/NotificationHub.Plugins.Sms.Kavenegar/
COPY Plugins/NotificationHub.Plugins.Sms.SmsIr/NotificationHub.Plugins.Sms.SmsIr.csproj Plugins/NotificationHub.Plugins.Sms.SmsIr/
COPY Plugins/NotificationHub.Plugins.InApp/NotificationHub.Plugins.InApp.csproj Plugins/NotificationHub.Plugins.InApp/
COPY Plugins/NotificationHub.Plugins.Chat.Slack/NotificationHub.Plugins.Chat.Slack.csproj Plugins/NotificationHub.Plugins.Chat.Slack/
COPY Plugins/NotificationHub.Plugins.Chat.WhatsApp/NotificationHub.Plugins.Chat.WhatsApp.csproj Plugins/NotificationHub.Plugins.Chat.WhatsApp/
COPY Plugins/NotificationHub.Plugins.Push.Fcm/NotificationHub.Plugins.Push.Fcm.csproj Plugins/NotificationHub.Plugins.Push.Fcm/

RUN dotnet restore

COPY . .
WORKDIR /src/src/NotificationHub.Host
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage (SEC-18)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Non-root user
RUN groupadd -r appuser && useradd -r -g appuser -d /app -s /sbin/nologin appuser \
    && mkdir -p /app && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "NotificationHub.Host.dll"]
