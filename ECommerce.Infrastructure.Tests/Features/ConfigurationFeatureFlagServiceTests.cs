using ECommerce.Application.Abstractions.Features;
using ECommerce.Infrastructure.Features;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Tests.Features;

public sealed class ConfigurationFeatureFlagServiceTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void IsEnabled_ReturnsConfiguredValueOrFalse(string? configuredValue, bool expected)
    {
        var values = new Dictionary<string, string?>();
        if (configuredValue is not null)
            values[$"FeatureFlags:{FeatureFlagNames.AdminDashboard}"] = configuredValue;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var service = new ConfigurationFeatureFlagService(configuration);

        var result = service.IsEnabled(FeatureFlagNames.AdminDashboard);

        result.Should().Be(expected);
    }
}
