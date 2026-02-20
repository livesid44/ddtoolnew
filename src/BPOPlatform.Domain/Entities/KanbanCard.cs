using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Entities;

/// <summary>
/// A task card on the Kanban board for a BPO process.
/// Cards move through columns: Todo → InProgress → Done.
/// </summary>
public class KanbanCard : BaseEntity
{
    public Guid ProcessId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>Kanban column name, e.g. "Todo", "InProgress", "Done".</summary>
    public string Column { get; private set; } = KanbanColumns.Todo;

    public TaskPriority Priority { get; private set; } = TaskPriority.Medium;
    public string AssignedTo { get; private set; } = string.Empty;

    /// <summary>Display order within the column (lower = higher on board).</summary>
    public int Position { get; private set; }

    private KanbanCard() { }

    public static KanbanCard Create(
        Guid processId,
        string title,
        string description,
        string column,
        TaskPriority priority,
        string assignedTo,
        int position = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new KanbanCard
        {
            ProcessId = processId,
            Title = title,
            Description = description,
            Column = column,
            Priority = priority,
            AssignedTo = assignedTo,
            Position = position
        };
    }

    public void Update(string title, string description, TaskPriority priority, string assignedTo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
        Description = description;
        Priority = priority;
        AssignedTo = assignedTo;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Move(string newColumn, int newPosition)
    {
        Column = newColumn;
        Position = newPosition;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>Well-known Kanban column names.</summary>
public static class KanbanColumns
{
    public const string Todo = "Todo";
    public const string InProgress = "InProgress";
    public const string Done = "Done";

    public static readonly IReadOnlyList<string> All = [Todo, InProgress, Done];
}
