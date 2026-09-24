using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Categories;
using ECommerce.Application.Categories.Handlers;
using ECommerce.Domain.Entities;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Categories;

public sealed class CategoryHandlersTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Create_WhenNameIsAvailable_PersistsCategory()
    {
        var handler = new CreateCategoryHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CreateCategoryCommand("Áudio e Vídeo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Slug.Should().Be("audio-e-video");
        _repository.Verify(repository => repository.AddAsync(
            It.Is<Category>(category => category.Slug == "audio-e-video"),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenSlugAlreadyExists_ReturnsFailureWithoutCommit()
    {
        _repository.Setup(repository => repository.ExistsBySlugAsync(
                "audio",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new CreateCategoryHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CreateCategoryCommand("Audio"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _repository.Verify(repository => repository.AddAsync(
            It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenCategoryExists_UpdatesAndCommits()
    {
        var category = Category.Create("Audio").Value!;
        _repository.Setup(repository => repository.GetByIdAsync(
                category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = new UpdateCategoryHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new UpdateCategoryCommand(category.Id, "Casa e Jardim"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Slug.Should().Be("casa-e-jardim");
        _repository.Verify(repository => repository.Update(category), Times.Once);
        _unitOfWork.Verify(unit => unit.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenCategoryExists_SoftDeletesAndCommits()
    {
        var category = Category.Create("Audio").Value!;
        _repository.Setup(repository => repository.GetByIdAsync(
                category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = new DeleteCategoryHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();
        _repository.Verify(repository => repository.Update(category), Times.Once);
        _unitOfWork.Verify(unit => unit.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_MapsCategoriesInRepositoryOrder()
    {
        Category[] categories = [Category.Create("Audio").Value!, Category.Create("Video").Value!];
        _repository.Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);
        var handler = new GetCategoriesHandler(_repository.Object);

        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(category => category.Name).Should().ContainInOrder("Audio", "Video");
    }

    [Fact]
    public async Task GetById_WhenCategoryDoesNotExist_ReturnsFailure()
    {
        var handler = new GetCategoryByIdHandler(_repository.Object);

        var result = await handler.Handle(new GetCategoryByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Category not found.");
    }
}
