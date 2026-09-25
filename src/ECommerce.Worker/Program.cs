using ECommerce.Worker.Persistence;
using ECommerce.Worker.Processing;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("WorkerDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkerDatabase is required.");

builder.Services.AddDbContext<WorkerDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", WorkerDbContext.SchemaName)));
builder.Services.AddScoped<OrderIntegrationEventProcessor>();

var host = builder.Build();
await host.RunAsync();
