using ECommerce.Application.Abstractions.Email;
using ECommerce.Shared.Results;

namespace ECommerce.Infrastructure.Email;

public sealed class DisabledUserEmailSender : IUserEmailSender
{
    public Task<Result> SendAsync(
        UserEmailDelivery delivery,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure("E-mail delivery is disabled."));
}
