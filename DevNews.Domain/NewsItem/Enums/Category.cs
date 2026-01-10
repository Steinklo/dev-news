namespace DevNews.Domain.NewsItem.Enums;

/// <summary>
/// Primary categories for developer news, ordered by priority.
/// Matches the CLAUDE.md specification.
/// </summary>
public enum CategoryEnum
{
    SecurityAndVulnerabilities = 1,
    ProgrammingLanguagesAndRuntimes = 2,
    FrameworksAndLibraries = 3,
    CloudAndInfrastructure = 4,
    DevOpsCiCdObservabilityTesting = 5,
    AiMlDeveloperTooling = 6,
    PerformanceAndArchitecturePatterns = 7,
    DeveloperToolsIdesProductivity = 8
}
