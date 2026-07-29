namespace DeskNote.App.Models;

public sealed record TodoInput(string Title, string? Note);

public sealed record TodoValidationResult(
    bool IsValid,
    string Title,
    string? Note,
    string? ErrorMessage);
