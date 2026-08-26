# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project graph for restore layer caching
COPY NotificationHub.sln ./
# Central package/build props must exist before restore (TargetFramework + package versions)
COPY Directory.Build.props Directory.Packages.props ./
COPY src/NotificationHub.Abstractions/NotificationHub.Abstractions.csproj src/NotificationHub.Abstractions/
COPY src/NotificationHub.Core/NotificationHub.Core.csproj src/NotificationHub.Core/
COPY src/NotificationHub.Host/NotificationHub.Host.csproj src/NotificationHub.Host/
COPY src/NotificationHub.Sdk/NotificationHub.Sdk.csproj src/NotificationHub.Sdk/
COPY src/NotificationHub.Application/NotificationHub.Application.csproj src/NotificationHub.Application/
COPY src/NotificationHub.Infrastructure/NotificationHub.Infrastructure.csproj src/NotificationHub.Infrastructure/
COPY src/NotificationHub.ServiceDefaults/NotificationHub.ServiceDefaults.csproj src/NotificationHub.ServiceDefaults/
COPY tests/NotificationHub.Core.Tests/NotificationHub.Core.Tests.csproj tests/NotificationHub.Core.Tests/
COPY tools/loadtest/NotificationHub.LoadTest.csproj tools/loadtest/

COPY Plugins/NotificationHub.Plugins.Email.SendGrid/NotificationHub.Plugins.Email.SendGrid.csproj Plugins/NotificationHub.Plugins.Email.SendGrid/
COPY Plugins/NotificationHub.Plugins.Email.Smtp/NotificationHub.Plugins.Email.Smtp.csproj Plugins/NotificationHub.Plugins.Email.Smtp/
COPY Plugins/NotificationHub.Plugins.Email.Resend/NotificationHub.Plugins.Email.Resend.csproj Plugins/NotificationHub.Plugins.Email.Resend/
COPY Plugins/NotificationHub.Plugins.Email.Ses/NotificationHub.Plugins.Email.Ses.csproj Plugins/NotificationHub.Plugins.Email.Ses/
COPY Plugins/NotificationHub.Plugins.Sms.Kavenegar/NotificationHub.Plugins.Sms.Kavenegar.csproj Plugins/NotificationHub.Plugins.Sms.Kavenegar/
COPY Plugins/NotificationHub.Plugins.Sms.SmsIr/NotificationHub.Plugins.Sms.SmsIr.csproj Plugins/NotificationHub.Plugins.Sms.SmsIr/
COPY Plugins/NotificationHub.Plugins.Sms.Twilio/NotificationHub.Plugins.Sms.Twilio.csproj Plugins/NotificationHub.Plugins.Sms.Twilio/
COPY Plugins/NotificationHub.Plugins.InApp/NotificationHub.Plugins.InApp.csproj Plugins/NotificationHub.Plugins.InApp/
COPY Plugins/NotificationHub.Plugins.Chat.Slack/NotificationHub.Plugins.Chat.Slack.csproj Plugins/NotificationHub.Plugins.Chat.Slack/
COPY Plugins/NotificationHub.Plugins.Chat.WhatsApp/NotificationHub.Plugins.Chat.WhatsApp.csproj Plugins/NotificationHub.Plugins.Chat.WhatsApp/
COPY Plugins/NotificationHub.Plugins.Chat.Telegram/NotificationHub.Plugins.Chat.Telegram.csproj Plugins/NotificationHub.Plugins.Chat.Telegram/
COPY Plugins/NotificationHub.Plugins.Chat.Discord/NotificationHub.Plugins.Chat.Discord.csproj Plugins/NotificationHub.Plugins.Chat.Discord/
COPY Plugins/NotificationHub.Plugins.Chat.Teams/NotificationHub.Plugins.Chat.Teams.csproj Plugins/NotificationHub.Plugins.Chat.Teams/
COPY Plugins/NotificationHub.Plugins.Push.Fcm/NotificationHub.Plugins.Push.Fcm.csproj Plugins/NotificationHub.Plugins.Push.Fcm/
COPY Plugins/NotificationHub.Plugins.Push.Expo/NotificationHub.Plugins.Push.Expo.csproj Plugins/NotificationHub.Plugins.Push.Expo/

# Restore only the Host graph (pulls all plugin + ServiceDefaults refs); avoids requiring full solution source at this layer
RUN dotnet restore src/NotificationHub.Host/NotificationHub.Host.csproj

COPY . .
WORKDIR /src/src/NotificationHub.Host
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage (SEC-18)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

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
