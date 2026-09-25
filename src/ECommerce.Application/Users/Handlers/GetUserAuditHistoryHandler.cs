using System.Text.Json;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users.Dtos;
using ECommerce.Shared.Pagination;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Users.Handlers;

public sealed class GetUserAuditHistoryHandler(
    IUserRepository userRepository,
    IUserAuditRepository auditRepository)
    : IRequestHandler<GetUserAuditHistoryQuery, Result<PagedResult<UserAuditEntryDto>>>
{
    public async Task<Result<PagedResult<UserAuditEntryDto>>> Handle(
        GetUserAuditHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result<PagedResult<UserAuditEntryDto>>.Failure("User not found.");

        var history = await auditRepository.GetByUserIdAsync(
            request.UserId,
            request.Page,
            request.PageSize,
            cancellationToken);
        var response = new PagedResult<UserAuditEntryDto>(
            [.. history.Items.Select(ToDto)],
            history.Page,
            history.PageSize,
            history.TotalCount);
        return Result<PagedResult<UserAuditEntryDto>>.Success(response);
    }

    private static UserAuditEntryDto ToDto(ECommerce.Domain.Entities.UserAuditEntry entry)
    {
        using var document = JsonDocument.Parse(entry.ChangesJson);
        return new UserAuditEntryDto(
            entry.Id,
            entry.UserId,
            entry.ActorUserId,
            entry.Action,
            document.RootElement.Clone(),
            entry.OccurredAt);
    }
}
