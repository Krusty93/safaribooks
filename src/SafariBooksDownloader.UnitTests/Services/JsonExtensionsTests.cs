using System.Text.Json;

namespace SafariBooksDownloader.UnitTests.Services;

public class JsonExtensionsTests
{
    [Fact]
    public void GetStringOrEmpty_WithValidProperty_ReturnsPropertyValue()
    {
        // Arrange
        var json = """{"description": "This is a test description"}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetStringOrEmpty("description");

        // Assert
        Assert.Equal("This is a test description", result);
    }

    [Fact]
    public void GetStringOrEmpty_WithMissingProperty_ReturnsEmpty()
    {
        // Arrange
        var json = """{"other": "value"}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetStringOrEmpty("description");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetStringOrEmpty_WithNullProperty_ReturnsEmpty()
    {
        // Arrange
        var json = """{"description": null}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetStringOrEmpty("description");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetArrayStrings_WithValidArrayAndProperty_ReturnsStringArray()
    {
        // Arrange
        var json = """
        {
            "authors": [
                {"name": "Author One", "id": 1},
                {"name": "Author Two", "id": 2},
                {"name": "Author Three", "id": 3}
            ]
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetArrayStrings("authors", "name");

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal("Author One", result[0]);
        Assert.Equal("Author Two", result[1]);
        Assert.Equal("Author Three", result[2]);
    }

    [Fact]
    public void GetArrayStrings_WithMissingArray_ReturnsEmptyArray()
    {
        // Arrange
        var json = """{"other": "value"}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetArrayStrings("authors", "name");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetArrayStrings_WithNullArray_ReturnsEmptyArray()
    {
        // Arrange
        var json = """{"authors": null}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetArrayStrings("authors", "name");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetArrayStrings_WithEmptyArray_ReturnsEmptyArray()
    {
        // Arrange
        var json = """{"authors": []}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetArrayStrings("authors", "name");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetArrayStrings_WithMixedObjectsAndMissingProperties_ReturnsOnlyValidValues()
    {
        // Arrange
        var json = """
        {
            "authors": [
                {"name": "Author One", "id": 1},
                {"id": 2},
                {"name": "Author Three", "id": 3},
                "invalid-item"
            ]
        }
        """;
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetArrayStrings("authors", "name");

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Equal("Author One", result[0]);
        Assert.Equal("Author Three", result[1]);
    }

    [Fact]
    public void GetArrayStrings_WithNonArrayValue_ReturnsEmptyArray()
    {
        // Arrange
        var json = """{"authors": "not-an-array"}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetArrayStrings("authors", "name");

        // Assert
        Assert.Empty(result);
    }
}