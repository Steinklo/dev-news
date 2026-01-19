namespace DevNews.Domain.VideoContent.Enums;

/// <summary>
/// Status of a VideoContent through its lifecycle.
/// </summary>
public enum VideoStatusEnum
{
    Draft = 1,           // Script generated, video not yet created
    VideoGenerated = 2,  // Video file created, not yet validated
    AIValidated = 3,     // Passed AI validation, ready for publishing
    Published = 4,       // Successfully published to platforms
    Rejected = 5,        // Failed AI validation
    Failed = 6           // Technical failure during generation
}
