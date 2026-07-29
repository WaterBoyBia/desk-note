using DeskNote.App.Validation;
using Xunit;

namespace DeskNote.Tests.Validation;

public sealed class TodoInputValidatorTests
{
    [Fact]
    public void Validate_TrimsValidInput()
    {
        var result = TodoInputValidator.Validate("  Read book  ", "  Chapter 1  ");

        Assert.True(result.IsValid);
        Assert.Equal("Read book", result.Title);
        Assert.Equal("Chapter 1", result.Note);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyTitle(string title)
    {
        var result = TodoInputValidator.Validate(title, null);

        Assert.False(result.IsValid);
        Assert.Equal("待办标题不能为空。", result.ErrorMessage);
    }

    [Fact]
    public void Validate_RejectsTitleLongerThanTwoHundredCharacters()
    {
        var result = TodoInputValidator.Validate(new string('a', 201), null);

        Assert.False(result.IsValid);
        Assert.Equal("待办标题不能超过 200 个字符。", result.ErrorMessage);
    }

    [Fact]
    public void Validate_RejectsNoteLongerThanTwoThousandCharacters()
    {
        var result = TodoInputValidator.Validate("Valid", new string('a', 2001));

        Assert.False(result.IsValid);
        Assert.Equal("待办备注不能超过 2000 个字符。", result.ErrorMessage);
    }
}
