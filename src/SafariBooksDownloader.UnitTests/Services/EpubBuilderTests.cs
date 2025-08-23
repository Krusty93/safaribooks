using SafariBooksDownloader.Services;
using System.Text.Json;

namespace SafariBooksDownloader.UnitTests.Services;

public class EpubBuilderTests
{
    [Fact]
    public void ContainerXml_ReturnsValidEpubContainer()
    {
        // Act
        var result = EpubBuilder.ContainerXml;

        // Assert
        Assert.Contains("<?xml version=\"1.0\"?>", result);
        Assert.Contains("<container version=\"1.0\"", result);
        Assert.Contains("full-path=\"OEBPS/content.opf\"", result);
        Assert.Contains("media-type=\"application/oebps-package+xml\"", result);
    }

    [Fact]
    public void BuildContentOpf_GeneratesValidOpfContent()
    {
        // Arrange
        var id = "test-book-id";
        var title = "Test Book Title";
        var authors = new[] { "Author One", "Author Two" };
        var description = "Test book description";
        var subjects = new[] { "Programming", "Testing" };
        var publishers = new[] { "Test Publisher" };
        var rights = "All rights reserved";
        var issued = "2024-01-01";
        var coverItemId = "cover-id";
        var manifest = "<item id=\"test1\" href=\"test1.xhtml\" media-type=\"application/xhtml+xml\"/>";
        var spine = "<itemref idref=\"test1\"/>";
        var coverHref = "cover.xhtml";

        // Act
        var result = EpubBuilder.BuildContentOpf(id, title, authors, description, subjects, 
            publishers, rights, issued, coverItemId, manifest, spine, coverHref);

        // Assert
        Assert.Contains("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result);
        Assert.Contains("<package xmlns=\"http://www.idpf.org/2007/opf\"", result);
        Assert.Contains($"<dc:title>{title}</dc:title>", result);
        Assert.Contains("<dc:creator>Author One</dc:creator>", result);
        Assert.Contains("<dc:creator>Author Two</dc:creator>", result);
        Assert.Contains($"<dc:description>{description}</dc:description>", result);
        Assert.Contains("<dc:subject>Programming</dc:subject>", result);
        Assert.Contains("<dc:subject>Testing</dc:subject>", result);
        Assert.Contains($"<dc:publisher>{publishers[0]}</dc:publisher>", result);
        Assert.Contains($"<dc:rights>{rights}</dc:rights>", result);
        Assert.Contains($"<dc:date>{issued}</dc:date>", result);
        Assert.Contains($"<dc:identifier id=\"bookid\">{id}</dc:identifier>", result);
        Assert.Contains($"<meta name=\"cover\" content=\"{coverItemId}\"/>", result);
        Assert.Contains(manifest, result);
        Assert.Contains(spine, result);
        Assert.Contains($"<reference href=\"{coverHref}\" title=\"Cover\" type=\"cover\"/>", result);
    }

    [Fact]
    public void BuildTocNcx_GeneratesValidNcxContent()
    {
        // Arrange
        var id = "test-book-id";
        var title = "Test Book Title";
        var authors = new[] { "Author One", "Author Two" };
        var navMap = "<navPoint id=\"navPoint-1\" playOrder=\"1\"><navLabel><text>Chapter 1</text></navLabel><content src=\"chapter1.xhtml\"/></navPoint>";

        // Act
        var result = EpubBuilder.BuildTocNcx(id, title, authors, navMap);

        // Assert
        Assert.Contains("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>", result);
        Assert.Contains("<!DOCTYPE ncx PUBLIC", result);
        Assert.Contains("<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\"", result);
        Assert.Contains($"<meta content=\"ID:ISBN:{id}\" name=\"dtb:uid\"/>", result);
        Assert.Contains($"<docTitle><text>{title}</text></docTitle>", result);
        Assert.Contains("<docAuthor><text>Author One, Author Two</text></docAuthor>", result);
        Assert.Contains(navMap, result);
    }

    [Fact]
    public void BuildNavMapFromChapters_GeneratesValidNavMap()
    {
        // Arrange
        var chapters = new List<ProcessedChapter>
        {
            new ProcessedChapter 
            { 
                Chapter = new Chapter { Title = "Chapter 1" }, 
                XhtmlFilename = "chapter1.xhtml" 
            },
            new ProcessedChapter 
            { 
                Chapter = new Chapter { Title = "Chapter 2" }, 
                XhtmlFilename = "chapter2.xhtml" 
            }
        };

        // Act
        var result = EpubBuilder.BuildNavMapFromChapters(chapters);

        // Assert
        Assert.Contains("<navPoint id=\"navPoint-1\" playOrder=\"1\">", result);
        Assert.Contains("<navLabel><text>Chapter 1</text></navLabel>", result);
        Assert.Contains("<content src=\"chapter1.xhtml\"/>", result);
        Assert.Contains("<navPoint id=\"navPoint-2\" playOrder=\"2\">", result);
        Assert.Contains("<navLabel><text>Chapter 2</text></navLabel>", result);
        Assert.Contains("<content src=\"chapter2.xhtml\"/>", result);
    }

    [Fact]
    public void BuildManifestAndSpine_WithChaptersOnly_GeneratesCorrectOutput()
    {
        // Arrange
        var chapters = new List<ProcessedChapter>
        {
            new ProcessedChapter 
            { 
                Chapter = new Chapter { Title = "Chapter 1" }, 
                XhtmlFilename = "chapter1.xhtml" 
            },
            new ProcessedChapter 
            { 
                Chapter = new Chapter { Title = "Chapter 2" }, 
                XhtmlFilename = "chapter2.xhtml" 
            }
        };
        var stylesCount = 2;
        var nonExistentImagesDir = "/non/existent/path";

        // Act
        var result = EpubBuilder.BuildManifestAndSpine(chapters, stylesCount, nonExistentImagesDir);

        // Assert
        Assert.Contains("<item id=\"chapter1\" href=\"chapter1.xhtml\" media-type=\"application/xhtml+xml\"/>", result.Manifest);
        Assert.Contains("<item id=\"chapter2\" href=\"chapter2.xhtml\" media-type=\"application/xhtml+xml\"/>", result.Manifest);
        Assert.Contains("<item id=\"Style00\" href=\"Styles/Style00.css\" media-type=\"text/css\"/>", result.Manifest);
        Assert.Contains("<item id=\"Style01\" href=\"Styles/Style01.css\" media-type=\"text/css\"/>", result.Manifest);
        
        Assert.Contains("<itemref idref=\"chapter1\"/>", result.Spine);
        Assert.Contains("<itemref idref=\"chapter2\"/>", result.Spine);
        
        Assert.Null(result.CoverImageId);
    }

    [Theory]
    [InlineData("Simple Title", "Simple Title")]
    [InlineData("Title with & ampersand", "Title with &amp; ampersand")]
    [InlineData("Title with <brackets>", "Title with &lt;brackets&gt;")]
    [InlineData("Title with \"quotes\"", "Title with &quot;quotes&quot;")]
    [InlineData("Title with 'apostrophes'", "Title with &apos;apostrophes&apos;")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void EscapeXml_HandlesSpecialCharacters(string input, string expected)
    {
        // We need to test the private EscapeXml method indirectly through BuildTocNcx
        // Arrange
        var id = "test-id";
        var authors = new[] { "Test Author" };
        var navMap = "";

        // Act
        var result = EpubBuilder.BuildTocNcx(id, input, authors, navMap);

        // Assert
        if (expected != null)
        {
            Assert.Contains($"<docTitle><text>{expected}</text></docTitle>", result);
        }
    }
}