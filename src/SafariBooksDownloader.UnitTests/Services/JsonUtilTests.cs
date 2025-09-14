using System.Text.Json;
using SafariBooksDownloader.Core.Services;

namespace SafariBooksDownloader.UnitTests.Services;

public class JsonUtilTests
{
    [Fact]
    public void GetPropertyOrDefault_WithValidProperty_ReturnsPropertyValue()
    {
        // Arrange
        var json = """{"name": "test-value", "number": 42}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetPropertyOrDefault("name", "default");

        // Assert
        Assert.Equal("test-value", result);
    }

    [Fact]
    public void GetPropertyOrDefault_WithMissingProperty_ReturnsDefault()
    {
        // Arrange
        var json = """{"name": "test-value"}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetPropertyOrDefault("missing", "default");

        // Assert
        Assert.Equal("default", result);
    }

    [Fact]
    public void GetPropertyOrDefault_WithNullProperty_ReturnsDefault()
    {
        // Arrange
        var json = """{"name": null}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetPropertyOrDefault("name", "default");

        // Assert
        Assert.Equal("default", result);
    }

    [Fact]
    public void GetPropertyOrDefault_WithNumberProperty_ReturnsStringValue()
    {
        // Arrange
        var json = """{"number": 42}""";
        var element = JsonDocument.Parse(json).RootElement;

        // Act
        var result = element.GetPropertyOrDefault("number", "default");

        // Assert
        Assert.Equal("42", result);
    }
}