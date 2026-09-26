namespace ECommerce.Domain.ValueObjects;

public static class MoneyConstraints
{
    public const int Precision = 18;
    public const int Scale = 2;
    public const decimal MaximumValue = 9999999999999999.99m;

    public static bool HasSupportedScale(decimal value) =>
        decimal.Round(value, Scale) == value;
}
