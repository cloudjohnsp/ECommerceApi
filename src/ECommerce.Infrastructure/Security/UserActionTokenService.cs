using System.Security.Cryptography;
using System.Text;
using ECommerce.Application.Abstractions.Security;
using Microsoft.AspNetCore.WebUtilities;

namespace ECommerce.Infrastructure.Security;

public sealed class UserActionTokenService : IUserActionTokenService
{
    public IssuedUserActionToken Issue()
    {
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new IssuedUserActionToken(rawToken, Hash(rawToken));
    }

    public string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
