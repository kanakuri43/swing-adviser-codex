using System.Xml.Linq;

namespace SwingAdviser.Infrastructure.Tests.Architecture;

public sealed class ArchitectureContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ProjectReferences_FollowTheLayerDependencyDirection()
    {
        AssertProjectReferences("SwingAdviser.Domain", []);
        AssertProjectReferences("SwingAdviser.Application", ["SwingAdviser.Domain"]);
        AssertProjectReferences(
            "SwingAdviser.Infrastructure",
            ["SwingAdviser.Application", "SwingAdviser.Domain"]);
        AssertProjectReferences("SwingAdviser.Presentation", ["SwingAdviser.Application"]);
        AssertProjectReferences(
            "SwingAdviser.Desktop",
            ["SwingAdviser.Infrastructure", "SwingAdviser.Presentation"]);
    }

    [Theory]
    [InlineData("SwingAdviser.Domain")]
    [InlineData("SwingAdviser.Application")]
    public void InnerLayers_DoNotReferenceTechnicalFrameworks(string projectName)
    {
        AssertSourceDoesNotContain(
            projectName,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.Sqlite",
            "System.Net.Http",
            "HttpClient",
            "System.Windows",
            "Prism.",
            "System.Diagnostics.Process",
            "ProcessStartInfo");
    }

    [Fact]
    public void Presentation_DoesNotReferenceDatabaseHttpCliOrInfrastructure()
    {
        AssertSourceDoesNotContain(
            "SwingAdviser.Presentation",
            "SwingAdviser.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.Sqlite",
            "System.Net.Http",
            "HttpClient",
            "System.Diagnostics.Process",
            "ProcessStartInfo");
    }

    private static void AssertProjectReferences(string projectName, string[] expectedReferences)
    {
        var projectPath = GetProjectPath(projectName);
        var project = XDocument.Load(projectPath);
        var actualReferences = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => Path.GetFileNameWithoutExtension(value!))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedReferences.Order(StringComparer.Ordinal), actualReferences);
    }

    private static void AssertSourceDoesNotContain(string projectName, params string[] forbiddenText)
    {
        var projectDirectory = Path.GetDirectoryName(GetProjectPath(projectName))!;
        var violations = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .SelectMany(path => forbiddenText
                .Where(text => File.ReadAllText(path).Contains(text, StringComparison.Ordinal))
                .Select(text => $"{Path.GetRelativePath(RepositoryRoot, path)} contains '{text}'"))
            .ToArray();

        Assert.Empty(violations);
    }

    private static string GetProjectPath(string projectName) =>
        Path.Combine(RepositoryRoot, "src", projectName, $"{projectName}.csproj");

    private static bool IsBuildOutput(string path)
    {
        var relativePath = Path.GetRelativePath(RepositoryRoot, path);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase) ||
            segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SwingAdviser.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the SwingAdviser solution root.");
    }
}
