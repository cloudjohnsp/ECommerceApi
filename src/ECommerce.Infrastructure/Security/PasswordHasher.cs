using ECommerce.Application.Abstractions.Security;
using ECommerce.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Infrastructure.Security;

public sealed class PasswordHasher(IPasswordHasher<User> identityPasswordHasher) : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return identityPasswordHasher.HashPassword(null!, password);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        PasswordVerificationResult result = identityPasswordHasher.VerifyHashedPassword(null!, hashedPassword, password);

        return result switch
        {
            PasswordVerificationResult.Success => true,
            PasswordVerificationResult.SuccessRehashNeeded => true,
            PasswordVerificationResult.Failed => false,
            _ => false
        };
    }
}
