using FluentAssertions;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Tests;

public class ResultPatternTests
{
    [Fact]
    public void Success_has_no_error_and_value()
    {
        var r = Result.Success(42);
        r.IsSuccess.Should().BeTrue();
        r.Error.Should().BeNull();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_requires_error_and_blocks_value()
    {
        var r = Result.Failure<int>(Errors.NotificationNotFound);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("notification.not_found");
        var act = () => _ = r.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_transforms_success_and_preserves_failure()
    {
        Result.Success(2).Map(x => x * 3).Value.Should().Be(6);
        var fail = Result.Failure<int>(Errors.CampaignNotFound).Map(x => x * 3);
        fail.IsFailure.Should().BeTrue();
        fail.Error!.Code.Should().Be("campaign.not_found");
    }

    [Fact]
    public void Bind_short_circuits_on_failure()
    {
        var called = false;
        var r = Result.Failure<int>(Errors.TemplateNotFound)
            .Bind(x =>
            {
                called = true;
                return Result.Success(x + 1);
            });
        called.Should().BeFalse();
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Match_covers_both_branches()
    {
        Result.Success(1).Match(v => v + 1, _ => -1).Should().Be(2);
        Result.Failure<int>(Errors.TenantForbidden).Match(v => v, e => e.Count).Should().Be(1);
    }

    [Fact]
    public void Multi_error_validation()
    {
        var r = Result.Failure(new[]
        {
            Error.Validation("validation.email", "required", "Email"),
            Error.Validation("validation.name", "required", "Name")
        });
        r.Errors.Should().HaveCount(2);
        r.Error!.PropertyName.Should().Be("Email");
    }

    [Fact]
    public void Ensure_converts_success_to_failure()
    {
        var r = Result.Success(5).Ensure(x => x > 10, Error.BusinessRule("too.small", "must be > 10"));
        r.IsFailure.Should().BeTrue();
        r.Error!.Type.Should().Be(ErrorType.BusinessRule);
    }

    [Fact]
    public void Implicit_conversions()
    {
        Result<int> fromValue = 7;
        fromValue.Value.Should().Be(7);
        Result<int> fromError = Errors.NotificationNotFound;
        fromError.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Invalid_construction_throws()
    {
        var act = () => Result.Failure(Array.Empty<Error>());
        act.Should().Throw<ArgumentException>();
    }
}
