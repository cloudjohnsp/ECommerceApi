namespace ECommerce.Infrastructure.Payments;

internal static class PaymentGatewayHttpMessageHandlerFactory
{
    public static HttpMessageHandler Create() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    };
}
