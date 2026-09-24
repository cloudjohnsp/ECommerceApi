using System.Text;
using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ECommerce.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher(
    ConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options) : IIntegrationEventPublisher, IAsyncDisposable
{
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

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = integrationEvent.Id.ToString(),
                Type = integrationEvent.Type,
                Timestamp = new AmqpTimestamp(new DateTimeOffset(integrationEvent.OccurredAt).ToUnixTimeSeconds())
            };
            var body = Encoding.UTF8.GetBytes(integrationEvent.Payload);
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
