using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Security;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
