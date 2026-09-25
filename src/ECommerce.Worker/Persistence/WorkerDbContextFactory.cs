using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Worker.Persistence;

public sealed class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ECOMMERCE_WORKER_DESIGN_TIME_CONNECTION_STRING")
            ?? "Host=localhost;Database=ecommerce;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", WorkerDbContext.SchemaName))
            .Options;

        return new WorkerDbContext(options);
    }
}
