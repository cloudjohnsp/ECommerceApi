namespace ECommerce.Application.Abstractions.Security;

public sealed record IssuedUserActionToken(string RawToken, string TokenHash);

public interface IUserActionTokenService
{
    IssuedUserActionToken Issue();
    string Hash(string rawToken);
}
