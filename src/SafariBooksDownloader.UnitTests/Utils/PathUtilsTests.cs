using SafariBooksDownloader.Utils;

namespace SafariBooksDownloader.UnitTests.Utils;

public class PathUtilsTests
{
    [Theory]
    [InlineData("valid_filename.txt", "valid_filename.txt")]
    [InlineData("file/with/slash", "file_with_slash")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    [InlineData("___multiple___underscores___", "multiple_underscores")]
    [InlineData("...leading.dots", "leading.dots")]
    [InlineData("trailing_underscores___", "trailing_underscores")]
    [InlineData(" spaces and dots. ", "spaces and dots")]
    public void CleanFileName_ValidatesAndCleansInput(string input, string expected)
    {
        // Act
        var result = PathUtils.CleanFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanFileName_TruncatesLongFileNames()
    {
        // Arrange
        var longFileName = new string('a', 150);

        // Act
        var result = PathUtils.CleanFileName(longFileName);

        // Assert
        Assert.Equal(100, result.Length);
        Assert.Equal(new string('a', 100), result);
    }

    [Theory]
    [InlineData("valid_id", "valid_id")]
    [InlineData("", "item")]
    [InlineData(null, "item")]
    [InlineData("123invalid", "item_123invalid")]
    [InlineData("valid-id_123", "valid-id_123")]
    [InlineData("invalid@chars#here", "invalid_chars_here")]
    [InlineData("___multiple___underscores___", "_multiple_underscores")]
    [InlineData("_underscore_start", "_underscore_start")]
    [InlineData("trailing_underscores___", "trailing_underscores")]
    public void CleanId_ValidatesAndCleansXmlIds(string input, string expected)
    {
        // Act
        var result = PathUtils.CleanId(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanId_EnsuresValidXmlIdStart()
    {
        // Arrange
        var invalidStart = "1invalid-start";

        // Act
        var result = PathUtils.CleanId(invalidStart);

        // Assert
        Assert.StartsWith("item_", result);
        Assert.Equal("item_1invalid-start", result);
    }

    [Fact]
    public void CleanId_HandlesEmptyResultAfterCleaning()
    {
        // Arrange - string that becomes empty after cleaning
        var invalidChars = "@#$%^&*()";

        // Act
        var result = PathUtils.CleanId(invalidChars);

        // Assert
        Assert.Equal("item", result);
    }
}