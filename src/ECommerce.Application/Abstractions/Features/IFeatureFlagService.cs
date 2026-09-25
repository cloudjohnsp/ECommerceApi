namespace ECommerce.Application.Abstractions.Features;

public interface IFeatureFlagService
{
    bool IsEnabled(string featureName);
}
