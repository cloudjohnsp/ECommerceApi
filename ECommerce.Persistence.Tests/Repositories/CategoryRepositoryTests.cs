using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class CategoryRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsActiveCategoriesOrderedByName()
    {
        await using var context = CreateContext();
        var video = Category.Create("Video").Value!;
        var audio = Category.Create("Audio").Value!;
        var inactive = Category.Create("Inactive").Value!;
        inactive.Deactivate();
        context.Categories.AddRange(video, inactive, audio);
        await context.SaveChangesAsync();
        var repository = new CategoryRepository(context);

        var result = await repository.GetAllAsync();

        result.Select(category => category.Name).Should().ContainInOrder("Audio", "Video");
    }

    [Fact]
    public async Task ExistsBySlugAsync_IncludesSoftDeletedCategories()
    {
        await using var context = CreateContext();
        var category = Category.Create("Audio").Value!;
        category.Deactivate();
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var repository = new CategoryRepository(context);

        var exists = await repository.ExistsBySlugAsync("audio");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsBySlugAsync_WhenExcludedIdMatches_ReturnsFalse()
    {
        await using var context = CreateContext();
        var category = Category.Create("Audio").Value!;
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var repository = new CategoryRepository(context);

        var exists = await repository.ExistsBySlugAsync("audio", category.Id);

        exists.Should().BeFalse();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
