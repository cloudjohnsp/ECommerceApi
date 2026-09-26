namespace ECommerce.Application.Security;

internal static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const string Pattern =
        "^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[@#$%^&+=!])(?=\\S+$).{8,20}$";
}
