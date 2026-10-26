using Etamid.SharedKernel;

namespace Etamid.Domain.Tests;

public class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        var error = Error.Conflict("PHONE_EXISTS", "phone");
        Result result = error;
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PHONE_EXISTS");
        result.Error.Field.Should().Be("phone");
    }

    [Fact]
    public void Generic_success_exposes_value()
    {
        Result<int> result = 42;
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Reading_value_of_failed_result_throws()
    {
        Result<int> result = Error.NotFound("NOT_FOUND");
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Failed_result_without_error_is_rejected()
    {
        var act = () => Result.Failure(Error.None);
        act.Should().Throw<InvalidOperationException>();
    }
}
