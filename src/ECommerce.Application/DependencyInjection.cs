using ECommerce.Application.Behaviors;
using ECommerce.Application.Users;
using ECommerce.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using FluentValidation;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Payments;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Email;
using ECommerce.Application.Abstractions.Orders;
using ECommerce.Application.Orders;

namespace ECommerce.Application;

public static class DependencyInjection
{
    public static void RegisterMapsterConfiguration(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
    }
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), ServiceLifetime.Transient);
        services.AddScoped<IPaymentCreationProcessor, PaymentCreationProcessor>();
        services.AddScoped<IUserEmailOutboxProcessor, UserEmailOutboxProcessor>();
        services.AddScoped<IOrderExpirationProcessor, OrderExpirationProcessor>();
        services.RegisterMapsterConfiguration();
        return services;
    }
}
