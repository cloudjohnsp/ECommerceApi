using ECommerce.Application.Abstractions.Features;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Features;

public sealed class ConfigurationFeatureFlagService(IConfiguration configuration) : IFeatureFlagService
{
    public bool IsEnabled(string featureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        return configuration.GetValue<bool>($"FeatureFlags:{featureName}");
    }
}
