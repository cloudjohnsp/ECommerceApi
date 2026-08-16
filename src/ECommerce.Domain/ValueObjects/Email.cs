using System.Net.Mail;
using ECommerce.Shared.Results;

namespace ECommerce.Domain.ValueObjects;

public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string value)
    {
        var normalizedValue = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return Result<Email>.Failure("E-mail is required.");
        }

        try
        {
            _ = new MailAddress(normalizedValue);
        }
        catch (FormatException)
        {
            return Result<Email>.Failure("E-mail is invalid.");
        }

        return Result<Email>.Success(new Email(normalizedValue));
    }

    public override string ToString() => Value;
}
