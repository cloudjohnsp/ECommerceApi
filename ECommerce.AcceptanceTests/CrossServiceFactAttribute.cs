namespace ECommerce.AcceptanceTests;

public sealed class CrossServiceFactAttribute : FactAttribute
{
    public CrossServiceFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_CROSS_SERVICE_ACCEPTANCE_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set RUN_CROSS_SERVICE_ACCEPTANCE_TESTS=true and start Docker to run this suite.";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CrossServiceCollection : ICollectionFixture<CrossServiceFixture>
{
    public const string Name = "cross-service-acceptance";
}
