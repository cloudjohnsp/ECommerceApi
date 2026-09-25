using ECommerce.Application.Auth.Dtos;
using ECommerce.Shared.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthTokensDto>>;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;

public sealed record ConfirmEmailCommand(string Token) : IRequest<Result>;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<Result>;

