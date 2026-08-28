using FluentAssertions;
using NotificationHub.Core.Expressions;

namespace NotificationHub.Core.Tests.Expressions;

public class SimpleExpressionEvaluatorTests
{
    private readonly SimpleExpressionEvaluator _sut = new();

    private static Dictionary<string, object?> Data(params (string k, object? v)[] pairs)
        => pairs.ToDictionary(x => x.k, x => x.v);

    [Theory]
    [InlineData("plan == \"pro\"", true)]
    [InlineData("plan != \"pro\"", false)]
    [InlineData("score >= 10", true)]
    [InlineData("score < 5", false)]
    [InlineData("plan == \"pro\" and score >= 10", true)]
    [InlineData("plan == \"free\" or score >= 10", true)]
    [InlineData("not (plan == \"free\")", true)]
    [InlineData("email contains \"@gmail.com\"", true)]
    [InlineData("email startsWith \"user\"", true)]
    [InlineData("exists phone", true)]
    [InlineData("exists missing", false)]
    public void TC_F_110_Expression_Cases(string expr, bool expected)
    {
        var data = Data(
            ("plan", "pro"),
            ("score", 12),
            ("email", "user@gmail.com"),
            ("phone", "+98912")
        );
        _sut.Evaluate(expr, data).Should().Be(expected);
    }

    [Fact]
    public void TC_E_110_InvalidExpression_ReturnsFalse()
    {
        _sut.Evaluate("plan ===", Data(("plan", "x"))).Should().BeFalse();
    }

    [Fact]
    public void TC_F_111_EmptyExpression_IsTrue()
    {
        _sut.Evaluate("", Data()).Should().BeTrue();
        _sut.Evaluate(null, Data()).Should().BeTrue();
    }
}
