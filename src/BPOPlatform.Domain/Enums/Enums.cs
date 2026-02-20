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
    Admin = 3,
    SuperAdmin = 4
}

/// <summary>Well-known role name constants used in policy and JWT claims.</summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string User = "User";
}

public enum IntakeStatus
{
    /// <summary>Chat is ongoing, meta fields still being collected.</summary>
    Draft = 0,
    /// <summary>Meta info submitted, ready for artifact upload.</summary>
    Submitted = 1,
    /// <summary>Artifacts uploaded, AI analysis has been run.</summary>
    Analysed = 2,
    /// <summary>Promoted to a full Process project.</summary>
    Promoted = 3
}
