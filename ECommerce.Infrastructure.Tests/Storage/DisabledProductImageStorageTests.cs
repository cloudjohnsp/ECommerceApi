using ECommerce.Infrastructure.Storage;
using FluentAssertions;

namespace ECommerce.Infrastructure.Tests.Storage;

public sealed class DisabledProductImageStorageTests
{
    [Fact]
    public async Task Upload_ReturnsExplicitFailure()
    {
        var storage = new DisabledProductImageStorage();
        await using var content = new MemoryStream([1]);

        var result = await storage.UploadAsync(
            Guid.NewGuid(), content, "image/png", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Product image storage is disabled.");
    }
}
