using ECommerce.Domain.Entities;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class CategoryTests
{
    [Fact]
    public void Create_WithValidName_NormalizesNameAndSlug()
    {
        var result = Category.Create("  Áudio e Vídeo  ");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Áudio e Vídeo");
        result.Value.Slug.Should().Be("audio-e-video");
        result.Value.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    public void Create_WithInvalidName_ReturnsFailure(string name)
    {
        var result = Category.Create(name);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Update_WhenActive_ChangesNameAndSlug()
    {
        var category = Category.Create("Audio").Value!;

        var result = category.Update("Casa & Jardim");

        result.IsSuccess.Should().BeTrue();
        category.Name.Should().Be("Casa & Jardim");
        category.Slug.Should().Be("casa-jardim");
        category.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_WhenInactive_ReturnsFailure()
    {
        var category = Category.Create("Audio").Value!;
        category.Deactivate();

        var result = category.Update("Video");

        result.IsFailure.Should().BeTrue();
        category.Name.Should().Be("Audio");
    }

    [Fact]
    public void Deactivate_IsIdempotent()
    {
        var category = Category.Create("Audio").Value!;

        category.Deactivate();
        var firstDeactivatedAt = category.DeactivatedAt;
        var result = category.Deactivate();

        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();
        category.DeactivatedAt.Should().Be(firstDeactivatedAt);
    }
}
