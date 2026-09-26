using ECommerce.Application.Behaviors;
using ECommerce.Shared.Results;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace ECommerce.Application.Tests.Behaviours;

public sealed class ValidationBehaviourTests
{
    [Fact]
    public async Task Handle_InvalidRequestReturningResult_ReturnsFailureWithoutCallingHandler()
    {
        var handlerCalled = false;
        var behavior = new ValidationBehaviour<ResultCommand, Result>([new ResultCommandValidator()]);

        var response = await behavior.Handle(
            new ResultCommand(string.Empty, 0),
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Result.Success());
            },
            CancellationToken.None);

        response.IsFailure.Should().BeTrue();
        response.Errors.Should().BeEquivalentTo("Name is required.", "Quantity must be greater than zero.");
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_InvalidRequestReturningGenericResult_PreservesDistinctErrors()
    {
        var behavior = new ValidationBehaviour<ResultCommand, Result<string>>(
            [new ResultCommandValidator(), new DuplicateNameValidator()]);

        var response = await behavior.Handle(
            new ResultCommand(string.Empty, 0),
            _ => Task.FromResult(Result<string>.Success("not-called")),
            CancellationToken.None);

        response.IsFailure.Should().BeTrue();
        response.Errors.Should().BeEquivalentTo("Name is required.", "Quantity must be greater than zero.");
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsHandler()
    {
        var behavior = new ValidationBehaviour<ResultCommand, Result>([new ResultCommandValidator()]);

        var response = await behavior.Handle(
            new ResultCommand("Keyboard", 1),
            _ => Task.FromResult(Result.Success()),
            CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
    }

    private sealed record ResultCommand(string Name, int Quantity)
        : IRequest<Result>, IRequest<Result<string>>;

    private sealed class ResultCommandValidator : AbstractValidator<ResultCommand>
    {
        public ResultCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty().WithMessage("Name is required.");
            RuleFor(command => command.Quantity).GreaterThan(0)
                .WithMessage("Quantity must be greater than zero.");
        }
    }

    private sealed class DuplicateNameValidator : AbstractValidator<ResultCommand>
    {
        public DuplicateNameValidator()
        {
            RuleFor(command => command.Name).NotEmpty().WithMessage("Name is required.");
        }
    }
}
