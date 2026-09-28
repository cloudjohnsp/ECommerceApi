using System.Text;
using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ECommerce.Shared.Messaging;
using System.Text.Json;

namespace ECommerce.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher(
    ConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options) : IIntegrationEventPublisher, IAsyncDisposable
{
    internal const string ContractVersionHeaderName = "x-contract-version";
    internal const string ContractVersion = "1";

    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public async Task<Result> PublishAsync(
        IntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            await using var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
            await channel.ExchangeDeclareAsync(
                _options.ExchangeName,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var properties = CreateProperties(integrationEvent);
            var body = CreateBody(integrationEvent);
            await channel.BasicPublishAsync(
                _options.ExchangeName,
                integrationEvent.Type,
                mandatory: true,
                properties,
                body,
                cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure($"RabbitMQ publication failed: {exception.Message}");
        }
    }

    internal static BasicProperties CreateProperties(IntegrationEvent integrationEvent) => new()
    {
        ContentType = "application/json",
        DeliveryMode = DeliveryModes.Persistent,
        MessageId = integrationEvent.Id.ToString(),
        Type = integrationEvent.Type,
        Timestamp = new AmqpTimestamp(new DateTimeOffset(integrationEvent.OccurredAt).ToUnixTimeSeconds()),
        Headers = new Dictionary<string, object?>
        {
            [ContractVersionHeaderName] = Encoding.UTF8.GetBytes(ContractVersion),
            ["x-correlation-id"] = Encoding.UTF8.GetBytes(NormalizeCorrelationId(integrationEvent))
        }
    };

    internal static byte[] CreateBody(IntegrationEvent integrationEvent)
    {
        var envelope = IntegrationEventEnvelope.Create(
            integrationEvent.Id,
            integrationEvent.Type,
            new DateTimeOffset(integrationEvent.OccurredAt),
            NormalizeCorrelationId(integrationEvent),
            integrationEvent.Payload);
        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    private static string NormalizeCorrelationId(IntegrationEvent integrationEvent) =>
        string.IsNullOrWhiteSpace(integrationEvent.CorrelationId)
            ? integrationEvent.Id.ToString("N")
            : integrationEvent.CorrelationId.Trim();

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _connectionLock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            if (_connection is not null)
                await _connection.DisposeAsync();
            _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
