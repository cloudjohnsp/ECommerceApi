using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Users.Dtos;

public sealed record UserDto(Guid Id, string FirstName, string LastName, string Email, UserRole Role, bool IsActive) { }

