using DeskNote.App.Models;

namespace DeskNote.App.Validation;

public static class TodoInputValidator
{
    public static TodoValidationResult Validate(string? title, string? note)
    {
        var normalizedTitle = title?.Trim() ?? string.Empty;
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (normalizedTitle.Length == 0)
        {
            return Invalid(normalizedTitle, normalizedNote, "待办标题不能为空。");
        }

        if (normalizedTitle.Length > 200)
        {
            return Invalid(normalizedTitle, normalizedNote, "待办标题不能超过 200 个字符。");
        }

        if (normalizedNote is { Length: > 2000 })
        {
            return Invalid(normalizedTitle, normalizedNote, "待办备注不能超过 2000 个字符。");
        }

        return new TodoValidationResult(true, normalizedTitle, normalizedNote, null);
    }

    private static TodoValidationResult Invalid(string title, string? note, string message) =>
        new(false, title, note, message);
}
