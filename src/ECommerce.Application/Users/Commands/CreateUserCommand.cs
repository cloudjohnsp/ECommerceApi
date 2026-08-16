using ECommerce.Application.Users.Dtos;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Users.Commands;

public sealed record CreateUserCommand(string FirstName, string LastName, string Email, string Password, UserRole Role = UserRole.Customer) : IRequest<Result<UserDto>>;

