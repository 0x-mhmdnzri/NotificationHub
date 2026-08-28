using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Application.Features.Templates.Delete;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Application.Tests;

public class AuthorizationBehaviorTests
{
    [Fact]
    public async Task DeleteTemplate_WithoutAdmin_ThrowsAuthorizationException()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IRequestContext>(_ => new TestRequestContext(true, null, AppRoles.Reader));
        services.AddScoped<ITemplateStore, NotificationHub.Core.Templates.InMemoryTemplateStore>();
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        services.AddScoped(_ => db);
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        var act = async () => await sender.Send(new DeleteTemplateCommand("k", "email", "en", null));
        await act.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task DeleteTemplate_WithAdmin_DoesNotThrowAuth()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IRequestContext>(_ => new TestRequestContext(true, null, AppRoles.Admin));
        services.AddScoped<ITemplateStore, InMemoryTemplateStore>();
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        services.AddScoped(_ => db);
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Not found is ok — means auth passed
        var result = await sender.Send(new DeleteTemplateCommand("missing", "email", "en", null));
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
