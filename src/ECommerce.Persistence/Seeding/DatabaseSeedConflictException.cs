namespace ECommerce.Persistence.Seeding;

public sealed class DatabaseSeedConflictException(string message) : InvalidOperationException(message);
