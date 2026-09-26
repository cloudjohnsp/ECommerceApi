namespace ECommerce.Api.Contracts.Administration;

public sealed record SalesReportRequest(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TopProducts = 10);
