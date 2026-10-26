using Etamid.Application.Common.Behaviors;
using Etamid.Application.Common.Exceptions;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;

namespace Etamid.Application.Tests;

public class ValidationBehaviorTests
{
    private sealed record TestRequest(string Name) : IRequest<Result>;

    private sealed class TestValidator : AbstractValidator<TestRequest>
    {
        public TestValidator() => RuleFor(x => x.Name).NotEmpty().WithErrorCode("REQUIRED");
    }

    [Fact]
    public async Task Invalid_request_throws_with_field_and_code()
    {
        var behavior = new ValidationBehavior<TestRequest, Result>([new TestValidator()]);

        var act = () => behavior.Handle(new TestRequest(""), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<RequestValidationException>()).Which;
        ex.Failures.Should().ContainSingle()
            .Which.Should().Be(new ValidationFailureInfo("name", "REQUIRED"));
    }

    [Fact]
    public async Task Valid_request_continues_the_pipeline()
    {
        var behavior = new ValidationBehavior<TestRequest, Result>([new TestValidator()]);

        var result = await behavior.Handle(new TestRequest("ok"), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
