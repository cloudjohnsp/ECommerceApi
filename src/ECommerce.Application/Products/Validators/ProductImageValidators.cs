using ECommerce.Domain.Entities;
using FluentValidation;

namespace ECommerce.Application.Products.Validators;

public sealed class UploadProductImageValidator : AbstractValidator<UploadProductImageCommand>
{
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    public UploadProductImageValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.Content).Cascade(CascadeMode.Stop).NotNull()
            .Must(stream => stream.CanRead && stream.CanSeek)
            .WithMessage("Image content must be readable and seekable.");
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255);
        RuleFor(command => command.ContentType)
            .Must(contentType => AllowedContentTypes.Contains(contentType))
            .WithMessage("Image content type must be image/jpeg, image/png or image/webp.");
        RuleFor(command => command.SizeBytes)
            .InclusiveBetween(1, ProductImage.MaximumSizeBytes);
    }
}

public sealed class GetProductImagesValidator : AbstractValidator<GetProductImagesQuery>
{
    public GetProductImagesValidator() => RuleFor(query => query.ProductId).NotEmpty();
}
