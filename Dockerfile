# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
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

# Copy everything else and build
COPY . .
WORKDIR /src/src/NotificationHub.Host
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN groupadd -r appuser && useradd -r -g appuser appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "NotificationHub.Host.dll"]
