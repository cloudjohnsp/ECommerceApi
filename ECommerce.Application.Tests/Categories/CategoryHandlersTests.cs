using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Categories;
using ECommerce.Application.Categories.Dtos;
using ECommerce.Application.Categories.Handlers;
using ECommerce.Domain.Entities;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Categories;

public sealed class CategoryHandlersTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICategoryCache> _cache = new();

    [Fact]
    public async Task Create_WhenNameIsAvailable_PersistsCategory()
    {
        var handler = new CreateCategoryHandler(_repository.Object, _unitOfWork.Object, _cache.Object);

        var result = await handler.Handle(new CreateCategoryCommand("Áudio e Vídeo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Slug.Should().Be("audio-e-video");
        _repository.Verify(repository => repository.AcquireSlugLockAsync(
            "audio-e-video",
            It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(repository => repository.AddAsync(
            It.Is<Category>(category => category.Slug == "audio-e-video"),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(cache => cache.SetAsync(
            It.Is<CategoryDto>(category => category.Slug == "audio-e-video"),
            CancellationToken.None), Times.Once);
        _cache.Verify(cache => cache.RemoveAllAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Create_WhenSlugAlreadyExists_ReturnsFailureWithoutCommit()
    {
        _repository.Setup(repository => repository.ExistsBySlugAsync(
                "audio",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new CreateCategoryHandler(_repository.Object, _unitOfWork.Object, _cache.Object);

        var result = await handler.Handle(new CreateCategoryCommand("Audio"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _repository.Verify(repository => repository.AcquireSlugLockAsync(
            "audio",
            It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(repository => repository.AddAsync(
            It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.RollbackTransactionAsync(
            CancellationToken.None), Times.Once);
        _cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_WhenCategoryExists_UpdatesAndCommits()
    {
        var category = Category.Create("Audio").Value!;
        _repository.Setup(repository => repository.GetByIdForUpdateAsync(
                category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = new UpdateCategoryHandler(_repository.Object, _unitOfWork.Object, _cache.Object);

        var result = await handler.Handle(
            new UpdateCategoryCommand(category.Id, "Casa e Jardim"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Slug.Should().Be("casa-e-jardim");
        _repository.Verify(repository => repository.AcquireSlugLockAsync(
            "casa-e-jardim",
            It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(repository => repository.Update(category), Times.Once);
        _unitOfWork.Verify(
            unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(
            unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(
            unit => unit.RollbackTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _cache.Verify(cache => cache.SetAsync(
            It.Is<CategoryDto>(cached => cached.Id == category.Id && cached.Slug == "casa-e-jardim"),
            CancellationToken.None), Times.Once);
        _cache.Verify(cache => cache.RemoveAllAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenCategoryExists_SoftDeletesAndCommits()
    {
        var category = Category.Create("Audio").Value!;
        _repository.Setup(repository => repository.GetByIdForUpdateAsync(
                category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = new DeleteCategoryHandler(_repository.Object, _unitOfWork.Object, _cache.Object);

        var result = await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();
        _repository.Verify(repository => repository.Update(category), Times.Once);
        _unitOfWork.Verify(
            unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(
            unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(
            unit => unit.RollbackTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        _cache.Verify(cache => cache.RemoveAsync(category.Id, CancellationToken.None), Times.Once);
        _cache.Verify(cache => cache.RemoveAllAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GetAll_MapsCategoriesInRepositoryOrder()
    {
        Category[] categories = [Category.Create("Audio").Value!, Category.Create("Video").Value!];
        _repository.Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);
        var handler = new GetCategoriesHandler(_repository.Object, _cache.Object);

        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(category => category.Name).Should().ContainInOrder("Audio", "Video");
        _cache.Verify(cache => cache.SetAllAsync(
            It.Is<IReadOnlyCollection<CategoryDto>>(items => items.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WhenCacheContainsCategories_DoesNotQueryRepository()
    {
        IReadOnlyCollection<CategoryDto> cachedCategories =
        [
            new CategoryDto(
                Guid.NewGuid(), "Audio", "audio", true,
                DateTimeOffset.UtcNow, null)
        ];
        _cache.Setup(cache => cache.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedCategories);
        var handler = new GetCategoriesHandler(_repository.Object, _cache.Object);

        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.Value.Should().BeSameAs(cachedCategories);
        _repository.Verify(repository => repository.GetAllAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_WhenCategoryDoesNotExist_ReturnsFailure()
    {
        var handler = new GetCategoryByIdHandler(_repository.Object, _cache.Object);

        var result = await handler.Handle(new GetCategoryByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Category not found.");
    }

    [Fact]
    public async Task GetById_WhenCacheContainsCategory_DoesNotQueryRepository()
    {
        var cachedCategory = new CategoryDto(
            Guid.NewGuid(), "Audio", "audio", true,
            DateTimeOffset.UtcNow, null);
        _cache.Setup(cache => cache.GetAsync(
                cachedCategory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedCategory);
        var handler = new GetCategoryByIdHandler(_repository.Object, _cache.Object);

        var result = await handler.Handle(
            new GetCategoryByIdQuery(cachedCategory.Id),
            CancellationToken.None);

        result.Value.Should().BeSameAs(cachedCategory);
        _repository.Verify(repository => repository.GetByIdAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
