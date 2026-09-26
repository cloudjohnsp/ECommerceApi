using System.Xml.Linq;
using FluentAssertions;

namespace ECommerce.Api.Tests.Architecture;

public sealed class ProjectDependencyTests
{
    private static readonly string[] ProjectNames =
    [
        "ECommerce.Api",
        "ECommerce.Application",
        "ECommerce.Domain",
        "ECommerce.Infrastructure",
        "ECommerce.Persistence",
        "ECommerce.Shared",
        "ECommerce.Worker"
    ];

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

    [Theory]
    [MemberData(nameof(ProductionProjects))]
    public void ProductionProjects_DeclareEveryProjectUsedBySource(
        string projectPath,
        string projectName)
    {
        var root = FindSolutionRoot();
        var absoluteProjectPath = Path.Combine(root, projectPath);
        var projectDirectory = Path.GetDirectoryName(absoluteProjectPath)!;
        var project = XDocument.Load(absoluteProjectPath);
        var declaredReferences = project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!))
            .ToHashSet(StringComparer.Ordinal);

        var usedProjects = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .SelectMany(path => ProjectNames.Where(name =>
                name != projectName &&
                File.ReadAllText(path).Contains(name, StringComparison.Ordinal)))
            .ToHashSet(StringComparer.Ordinal);

        usedProjects.Should().BeSubsetOf(
            declaredReferences,
            "source-level dependencies must be explicit in the project file");
    }

    [Theory]
    [MemberData(nameof(ProductionProjects))]
    public void ProductionSource_DeclaresNamespaceOwnedByItsProject(
        string projectPath,
        string projectName)
    {
        var root = FindSolutionRoot();
        var projectDirectory = Path.GetDirectoryName(Path.Combine(root, projectPath))!;
        var invalidFiles = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Select(path => new
            {
                Path = path,
                Namespace = File.ReadLines(path)
                    .Select(line => line.TrimStart())
                    .FirstOrDefault(line => line.StartsWith("namespace ", StringComparison.Ordinal))
            })
            .Where(source => source.Namespace is not null &&
                !source.Namespace.StartsWith($"namespace {projectName}", StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(root, source.Path))
            .ToArray();

        invalidFiles.Should().BeEmpty(
            $"source files in {projectName} must declare a namespace owned by that project");
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
            ["ECommerce.Application", "ECommerce.Domain", "ECommerce.Shared"]
        },
        {
            Path.Combine("src", "ECommerce.Infrastructure", "ECommerce.Infrastructure.csproj"),
            ["ECommerce.Application", "ECommerce.Domain", "ECommerce.Shared"]
        },
        {
            Path.Combine("src", "ECommerce.Api", "ECommerce.Api.csproj"),
            ["ECommerce.Application", "ECommerce.Domain", "ECommerce.Infrastructure", "ECommerce.Persistence", "ECommerce.Shared"]
        },
        {
            Path.Combine("src", "ECommerce.Worker", "ECommerce.Worker.csproj"),
            ["ECommerce.Shared"]
        }
    };

    public static TheoryData<string, string> ProductionProjects() => new()
    {
        { Path.Combine("src", "ECommerce.Shared", "ECommerce.Shared.csproj"), "ECommerce.Shared" },
        { Path.Combine("src", "ECommerce.Domain", "ECommerce.Domain.csproj"), "ECommerce.Domain" },
        { Path.Combine("src", "ECommerce.Application", "ECommerce.Application.csproj"), "ECommerce.Application" },
        { Path.Combine("src", "ECommerce.Persistence", "ECommerce.Persistence.csproj"), "ECommerce.Persistence" },
        { Path.Combine("src", "ECommerce.Infrastructure", "ECommerce.Infrastructure.csproj"), "ECommerce.Infrastructure" },
        { Path.Combine("src", "ECommerce.Api", "ECommerce.Api.csproj"), "ECommerce.Api" },
        { Path.Combine("src", "ECommerce.Worker", "ECommerce.Worker.csproj"), "ECommerce.Worker" }
    };

    private static bool IsGeneratedPath(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ECommerceApi.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
