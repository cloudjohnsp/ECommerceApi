using ECommerce.Worker.Messaging;
using ECommerce.Worker.Notifications;
using ECommerce.Worker.Options;
using ECommerce.Worker.Persistence;
using ECommerce.Worker.Processing;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("WorkerDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:WorkerDatabase is required.");

builder.Services.AddDbContext<WorkerDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", WorkerDbContext.SchemaName)));
builder.Services.AddScoped<OrderIntegrationEventProcessor>();
builder.Services.AddScoped<NotificationOutboxProcessor>();

builder.Services.AddOptions<WorkerRabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(WorkerRabbitMqOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.HostName), "RabbitMq:HostName is required.")
    .Validate(options => options.Port > 0, "RabbitMq:Port must be positive.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "RabbitMq:UserName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMq:Password is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ExchangeName), "RabbitMq:ExchangeName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.QueueName), "RabbitMq:QueueName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterExchangeName), "RabbitMq:DeadLetterExchangeName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterQueueName), "RabbitMq:DeadLetterQueueName is required.")
    .Validate(options => options.DeliveryLimit > 0, "RabbitMq:DeliveryLimit must be positive.")
    .Validate(options => options.RetryDelaySeconds > 0, "RabbitMq:RetryDelaySeconds must be positive.")
    .Validate(options => options.ReconnectDelaySeconds > 0, "RabbitMq:ReconnectDelaySeconds must be positive.")
    .ValidateOnStart();
builder.Services.AddOptions<WorkerEmailOptions>()
    .Bind(builder.Configuration.GetSection(WorkerEmailOptions.SectionName))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host), "Email:Host is required.")
    .Validate(options => !options.Enabled || options.Port > 0, "Email:Port must be positive.")
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.FromAddress), "Email:FromAddress is required.")
    .ValidateOnStart();
builder.Services.AddOptions<NotificationProcessorOptions>()
    .Bind(builder.Configuration.GetSection(NotificationProcessorOptions.SectionName))
    .Validate(options => options.BatchSize > 0, "NotificationProcessor:BatchSize must be positive.")
    .Validate(options => options.PollIntervalSeconds > 0, "NotificationProcessor:PollIntervalSeconds must be positive.")
    .Validate(options => options.LockSeconds > 0, "NotificationProcessor:LockSeconds must be positive.")
    .Validate(options => options.MaximumAttempts > 0, "NotificationProcessor:MaximumAttempts must be positive.")
    .ValidateOnStart();

var rabbitMqOptions = builder.Configuration
    .GetSection(WorkerRabbitMqOptions.SectionName)
    .Get<WorkerRabbitMqOptions>() ?? new WorkerRabbitMqOptions();
builder.Services.AddSingleton(_ => new ConnectionFactory
{
    HostName = rabbitMqOptions.HostName,
    Port = rabbitMqOptions.Port,
    UserName = rabbitMqOptions.UserName,
    Password = rabbitMqOptions.Password,
    VirtualHost = rabbitMqOptions.VirtualHost,
    ClientProvidedName = rabbitMqOptions.ClientProvidedName,
    AutomaticRecoveryEnabled = false,
    ConsumerDispatchConcurrency = 1
});

var emailOptions = builder.Configuration
    .GetSection(WorkerEmailOptions.SectionName)
    .Get<WorkerEmailOptions>() ?? new WorkerEmailOptions();
if (emailOptions.Enabled)
    builder.Services.AddSingleton<IOrderEmailSender, SmtpOrderEmailSender>();
else
    builder.Services.AddSingleton<IOrderEmailSender, DisabledOrderEmailSender>();

builder.Services.AddHostedService<RabbitMqOrderConsumer>();
builder.Services.AddHostedService<NotificationDispatcher>();

var host = builder.Build();
await host.Services.InitializeWorkerDatabaseAsync();
await host.RunAsync();
