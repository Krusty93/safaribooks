using SafariBooksDownloader.Core.Services;
using Xunit;

namespace SafariBooksDownloader.UnitTests.Services;

public class HtmlProcessorTests : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ParseChapterHtml_WithHttpImageUrl_PreservesHttpUrl()
    {
        // Arrange
        var html = @"<html><body><img src=""https://example.com/image.png"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "https://learning.oreilly.com/api/v2/epubs/urn:orm:book:123/files/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
        Assert.Equal("https://example.com/image.png", globalImages[0]);
    }

    [Fact]
    public void ParseChapterHtml_WithFileProtocolUrl_ConvertsToHttps()
    {
        // Arrange
        var html = @"<html><body><img src=""file:///api/v2/epubs/urn:orm:book:123/files/assets/cover.png"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "https://learning.oreilly.com/api/v2/epubs/urn:orm:book:123/files/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
        Assert.Equal("https://learning.oreilly.com/api/v2/epubs/urn:orm:book:123/files/assets/cover.png", globalImages[0]);
    }

    [Fact]
    public void ParseChapterHtml_WithRelativeImageUrl_JoinsWithAssetBaseUrl()
    {
        // Arrange
        var html = @"<html><body><img src=""../assets/image.png"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "https://learning.oreilly.com/api/v2/epubs/urn:orm:book:123/files/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
        Assert.StartsWith("https://learning.oreilly.com/", globalImages[0]);
    }

    [Fact]
    public void ParseChapterHtml_WithAssetBaseUrlWithoutScheme_AddsHttpsScheme()
    {
        // Arrange - This simulates the cross-platform issue where AssetBaseUrl might be a path
        var html = @"<html><body><img src=""assets/image.png"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "/api/v2/epubs/urn:orm:book:123/files/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
        Assert.StartsWith("https://learning.oreilly.com/", globalImages[0]);
        Assert.DoesNotContain("file://", globalImages[0]);
    }

    [Fact]
    public void ParseChapterHtml_ReplacesImageSrcWithRelativePath()
    {
        // Arrange
        var html = @"<html><body><img src=""https://example.com/path/to/image.png"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "https://learning.oreilly.com/api/v2/epubs/urn:orm:book:123/files/" };
        var globalImages = new List<string>();

        // Act
        var (_, bodyXhtml) = processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert - The src in the output HTML should be relative
        Assert.Contains("Images/image.png", bodyXhtml);
        Assert.DoesNotContain("https://", bodyXhtml);
    }

    [Fact]
    public void ParseChapterHtml_DoesNotDuplicateImages()
    {
        // Arrange
        var html = @"<html><body>
            <img src=""https://example.com/image.png"" />
            <img src=""https://example.com/image.png"" />
        </body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "https://learning.oreilly.com/api/v2/epubs/urn:orm:book:123/files/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
    }

    [Fact]
    public void BuildXhtml_CreatesValidXhtmlStructure()
    {
        // Arrange
        var inlineCss = @"<link href=""Styles/Style00.css"" rel=""stylesheet"" type=""text/css"" />";
        var bodyHtml = @"<p>Test content</p>";

        // Act
        var result = HtmlProcessor.BuildXhtml(false, inlineCss, bodyHtml);

        // Assert
        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("<html", result);
        Assert.Contains("xmlns=\"http://www.w3.org/1999/xhtml\"", result);
        Assert.Contains(inlineCss, result);
        Assert.Contains(bodyHtml, result);
    }

    [Fact]
    public void BuildXhtml_WithKindleOption_AddsKindleCss()
    {
        // Arrange
        var inlineCss = @"<link href=""Styles/Style00.css"" rel=""stylesheet"" type=""text/css"" />";
        var bodyHtml = @"<p>Test content</p>";

        // Act
        var result = HtmlProcessor.BuildXhtml(true, inlineCss, bodyHtml);

        // Assert
        Assert.Contains("word-wrap:break-word", result);
        Assert.Contains("word-break:break-word", result);
    }

    [Fact]
    public void ParseChapterHtml_WithFileUrlWithLeadingSlash_RemovesDoubleSlashes()
    {
        // Arrange - Test the fix for double slashes bug
        var html = @"<html><body><img src=""file:////api/v2/epubs/book/files/image.png"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "https://learning.oreilly.com/api/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
        Assert.DoesNotContain("//api", globalImages[0]); // Should not have double slashes
        Assert.StartsWith("https://learning.oreilly.com/api", globalImages[0]);
    }

    [Fact]
    public void ParseChapterHtml_WithEmptyFileUrl_HandlesGracefully()
    {
        // Arrange - Test edge case with file:/// and no path
        var html = @"<html><body><img src=""file:///"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        var chapter = new Chapter { AssetBaseUrl = "https://learning.oreilly.com/api/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
        // Should return original file:/// since there's no valid path
        Assert.Equal("file:///", globalImages[0]);
    }

    [Fact]
    public void ParseChapterHtml_WithNonHttpAbsoluteBaseUrl_HandlesCorrectly()
    {
        // Arrange - Test that only non-absolute URIs are converted
        var html = @"<html><body><img src=""assets/image.png"" /></body></html>";
        var client = new ApiClient(_httpClient);
        var processor = new HtmlProcessor(client);
        // Use a relative path that should be converted
        var chapter = new Chapter { AssetBaseUrl = "/api/v2/book/" };
        var globalImages = new List<string>();

        // Act
        processor.ParseChapterHtml(html, "https://learning.oreilly.com", "123", chapter, false, new List<string>(), globalImages);

        // Assert
        Assert.Single(globalImages);
        Assert.StartsWith("https://learning.oreilly.com/", globalImages[0]);
        Assert.DoesNotContain("file://", globalImages[0]);
    }
}
