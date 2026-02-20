namespace BPOPlatform.Domain.Enums;

public enum ProcessStatus
{
    Draft = 0,
    InProgress = 1,
    UnderReview = 2,
    Approved = 3,
    Deployed = 4,
    Archived = 5
}

public enum ArtifactType
{
    Video = 0,
    Pdf = 1,
    Audio = 2,
    Transcription = 3,
    Spreadsheet = 4,
    Other = 5
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum UserRole
{
    Viewer = 0,
    Analyst = 1,
    Manager = 2,
    Admin = 3
}
