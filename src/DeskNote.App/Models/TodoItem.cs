namespace DeskNote.App.Models;

public sealed record TodoItem(
    Guid Id,
    string Title,
    string? Note,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
