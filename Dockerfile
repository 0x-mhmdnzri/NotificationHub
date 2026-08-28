# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project graph for restore layer caching
COPY NotificationHub.sln ./
# Central package/build props must exist before restore (TargetFramework + package versions)
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Kernel/NotificationHub.Abstractions/NotificationHub.Abstractions.csproj src/Kernel/NotificationHub.Abstractions/
COPY src/BuildingBlocks/NotificationHub.Core/NotificationHub.Core.csproj src/BuildingBlocks/NotificationHub.Core/
COPY src/Host/NotificationHub.Host/NotificationHub.Host.csproj src/Host/NotificationHub.Host/
COPY src/Clients/NotificationHub.Sdk/NotificationHub.Sdk.csproj src/Clients/NotificationHub.Sdk/
COPY src/BuildingBlocks/NotificationHub.Application/NotificationHub.Application.csproj src/BuildingBlocks/NotificationHub.Application/
COPY src/BuildingBlocks/NotificationHub.Infrastructure/NotificationHub.Infrastructure.csproj src/BuildingBlocks/NotificationHub.Infrastructure/
COPY src/Host/NotificationHub.ServiceDefaults/NotificationHub.ServiceDefaults.csproj src/Host/NotificationHub.ServiceDefaults/
COPY tests/NotificationHub.Core.Tests/NotificationHub.Core.Tests.csproj tests/NotificationHub.Core.Tests/
COPY tools/loadtest/NotificationHub.LoadTest.csproj tools/loadtest/

COPY Plugins/Email/NotificationHub.Plugins.Email.SendGrid/NotificationHub.Plugins.Email.SendGrid.csproj Plugins/Email/NotificationHub.Plugins.Email.SendGrid/
COPY Plugins/Email/NotificationHub.Plugins.Email.Smtp/NotificationHub.Plugins.Email.Smtp.csproj Plugins/Email/NotificationHub.Plugins.Email.Smtp/
COPY Plugins/Email/NotificationHub.Plugins.Email.Resend/NotificationHub.Plugins.Email.Resend.csproj Plugins/Email/NotificationHub.Plugins.Email.Resend/
COPY Plugins/Email/NotificationHub.Plugins.Email.Ses/NotificationHub.Plugins.Email.Ses.csproj Plugins/Email/NotificationHub.Plugins.Email.Ses/
COPY Plugins/Sms/NotificationHub.Plugins.Sms.Kavenegar/NotificationHub.Plugins.Sms.Kavenegar.csproj Plugins/Sms/NotificationHub.Plugins.Sms.Kavenegar/
COPY Plugins/Sms/NotificationHub.Plugins.Sms.SmsIr/NotificationHub.Plugins.Sms.SmsIr.csproj Plugins/Sms/NotificationHub.Plugins.Sms.SmsIr/
COPY Plugins/Sms/NotificationHub.Plugins.Sms.Twilio/NotificationHub.Plugins.Sms.Twilio.csproj Plugins/Sms/NotificationHub.Plugins.Sms.Twilio/
COPY Plugins/InApp/NotificationHub.Plugins.InApp/NotificationHub.Plugins.InApp.csproj Plugins/InApp/NotificationHub.Plugins.InApp/
COPY Plugins/Chat/NotificationHub.Plugins.Chat.Slack/NotificationHub.Plugins.Chat.Slack.csproj Plugins/Chat/NotificationHub.Plugins.Chat.Slack/
COPY Plugins/Chat/NotificationHub.Plugins.Chat.WhatsApp/NotificationHub.Plugins.Chat.WhatsApp.csproj Plugins/Chat/NotificationHub.Plugins.Chat.WhatsApp/
COPY Plugins/Chat/NotificationHub.Plugins.Chat.Telegram/NotificationHub.Plugins.Chat.Telegram.csproj Plugins/Chat/NotificationHub.Plugins.Chat.Telegram/
COPY Plugins/Chat/NotificationHub.Plugins.Chat.Discord/NotificationHub.Plugins.Chat.Discord.csproj Plugins/Chat/NotificationHub.Plugins.Chat.Discord/
COPY Plugins/Chat/NotificationHub.Plugins.Chat.Teams/NotificationHub.Plugins.Chat.Teams.csproj Plugins/Chat/NotificationHub.Plugins.Chat.Teams/
COPY Plugins/Push/NotificationHub.Plugins.Push.Fcm/NotificationHub.Plugins.Push.Fcm.csproj Plugins/Push/NotificationHub.Plugins.Push.Fcm/
COPY Plugins/Push/NotificationHub.Plugins.Push.Expo/NotificationHub.Plugins.Push.Expo.csproj Plugins/Push/NotificationHub.Plugins.Push.Expo/

# Restore only the Host graph (pulls all plugin + ServiceDefaults refs); avoids requiring full solution source at this layer
RUN dotnet restore src/Host/NotificationHub.Host/NotificationHub.Host.csproj

COPY . .
WORKDIR /src/src/Host/NotificationHub.Host
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
