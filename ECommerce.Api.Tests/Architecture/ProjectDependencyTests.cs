using System.Xml.Linq;
using FluentAssertions;

namespace ECommerce.Api.Tests.Architecture;

public sealed class ProjectDependencyTests
{
    [Theory]
    [InlineData("src/ECommerce.Domain/ECommerce.Domain.csproj")]
    [InlineData("src/ECommerce.Application/ECommerce.Application.csproj")]
    public void CoreProjects_DoNotReferenceAspNetCoreFramework(string projectPath)
    {
        var project = XDocument.Load(Path.Combine(FindSolutionRoot(), projectPath));

        var frameworkReferences = project
            .Descendants("FrameworkReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference));

        frameworkReferences.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ProjectReferences))]
    public void ProjectReferences_PreserveDependencyDirection(
        string projectPath,
        string[] expectedReferences)
    {
        var root = FindSolutionRoot();
        var project = XDocument.Load(Path.Combine(root, projectPath));

        var references = project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!))
            .Order(StringComparer.Ordinal)
            .ToArray();

        references.Should().Equal(expectedReferences.Order(StringComparer.Ordinal));
    }

    public static TheoryData<string, string[]> ProjectReferences() => new()
    {
        {
            Path.Combine("src", "ECommerce.Shared", "ECommerce.Shared.csproj"),
            []
        },
        {
            Path.Combine("src", "ECommerce.Domain", "ECommerce.Domain.csproj"),
            ["ECommerce.Shared"]
        },
        {
            Path.Combine("src", "ECommerce.Application", "ECommerce.Application.csproj"),
            ["ECommerce.Domain", "ECommerce.Shared"]
        },
        {
            Path.Combine("src", "ECommerce.Persistence", "ECommerce.Persistence.csproj"),
            ["ECommerce.Application", "ECommerce.Domain"]
        },
        {
            Path.Combine("src", "ECommerce.Infrastructure", "ECommerce.Infrastructure.csproj"),
            ["ECommerce.Application", "ECommerce.Shared"]
        },
        {
            Path.Combine("src", "ECommerce.Api", "ECommerce.Api.csproj"),
            ["ECommerce.Application", "ECommerce.Infrastructure", "ECommerce.Persistence"]
        },
        {
            Path.Combine("src", "ECommerce.Worker", "ECommerce.Worker.csproj"),
            ["ECommerce.Shared"]
        }
    };

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ECommerceApi.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
