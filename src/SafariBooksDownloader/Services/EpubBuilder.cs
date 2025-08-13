using System.Text;
using SafariBooksDownloader.Utils;

namespace SafariBooksDownloader.Services;

internal static class EpubBuilder
{
    public static string ContainerXml => 
        """<?xml version="1.0"?><container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml" /></rootfiles></container>""";

    public static (string Manifest, string Spine) BuildManifestAndSpine(
        List<ProcessedChapter> chapters,
        int stylesCount,
        string imagesDir)
    {
        var manifest = new StringBuilder();
        var spine = new StringBuilder();

        foreach (var c in chapters)
        {
            var id = PathUtils.CleanId(Path.GetFileNameWithoutExtension(c.XhtmlFilename));
            manifest.AppendLine($@"<item id=""{id}"" href=""{c.XhtmlFilename}"" media-type=""application/xhtml+xml""/>");
            spine.AppendLine($@"<itemref idref=""{id}""/>");
        }

        for (int i = 0; i < stylesCount; i++)
        {
            manifest.AppendLine($@"<item id=""Style{i:00}"" href=""Styles/Style{i:00}.css"" media-type=""text/css""/>");
        }

        if (Directory.Exists(imagesDir))
        {
            foreach (var imgFile in Directory.GetFiles(imagesDir))
            {
                var filename = Path.GetFileName(imgFile);
                var id = PathUtils.CleanId(Path.GetFileNameWithoutExtension(filename));
                var mediaType = GetImageMediaType(Path.GetExtension(filename));
                manifest.AppendLine($@"<item id=""{id}"" href=""Images/{filename}"" media-type=""{mediaType}""/>");
            }
        }

        return (manifest.ToString(), spine.ToString());
    }

    public static string BuildContentOpf(
        string id,
        string title,
        string[] authors,
        string description,
        string[] subjects,
        string[] publishers,
        string rights,
        string issued,
        string coverItemId,
        string manifest,
        string spine,
        string coverHref)
    {
        var authorsXml = string.Join("\n", authors.Select(a => $"<dc:creator>{EscapeXml(a)}</dc:creator>"));
        var subjectsXml = string.Join("\n", subjects.Select(s => $"<dc:subject>{EscapeXml(s)}</dc:subject>"));
        var publishersXml = string.Join(", ", publishers);

        return $@"
<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" unique-identifier=""bookid"" version=""2.0"">
<metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:opf=""http://www.idpf.org/2007/opf"">
<dc:title>{EscapeXml(title)}</dc:title>
{authorsXml}
<dc:description>{EscapeXml(description)}</dc:description>
{subjectsXml}
<dc:publisher>{EscapeXml(publishersXml)}</dc:publisher>
<dc:rights>{EscapeXml(rights)}</dc:rights>
<dc:language>en-US</dc:language>
<dc:date>{EscapeXml(issued)}</dc:date>
<dc:identifier id=""bookid"">{EscapeXml(id)}</dc:identifier>
<meta name=""cover"" content=""{coverItemId}""/>
</metadata>
<manifest>
<item id=""ncx"" href=""toc.ncx"" media-type=""application/x-dtbncx+xml""/>
{manifest}
</manifest>
<spine toc=""ncx"">{spine}</spine>
<guide><reference href=""{coverHref}"" title=""Cover"" type=""cover""/></guide>
</package>";
    }

    public static string BuildTocNcx(
        string id,
        string title,
        string[] authors,
        string navMap)
    {
        var authorsList = string.Join(", ", authors);

        return $@"
<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
<!DOCTYPE ncx PUBLIC ""-//NISO//DTD ncx 2005-1//EN"" ""http://www.daisy.org/z3986/2005/ncx-2005-1.dtd"">
<ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">
<head>
<meta content=""ID:ISBN:{EscapeXml(id)}"" name=""dtb:uid""/>
<meta content=""1"" name=""dtb:depth""/>
<meta content=""0"" name=""dtb:totalPageCount""/>
<meta content=""0"" name=""dtb:maxPageNumber""/>
</head>
<docTitle><text>{EscapeXml(title)}</text></docTitle>
<docAuthor><text>{EscapeXml(authorsList)}</text></docAuthor>
<navMap>{navMap}</navMap>
</ncx>";
    }

    public static string BuildNavMapFromChapters(List<ProcessedChapter> chapters)
    {
        var navMap = new StringBuilder();
        for (int i = 0; i < chapters.Count; i++)
        {
            var ch = chapters[i];
            var playOrder = i + 1;
            navMap.AppendLine($@"
<navPoint id=""navPoint-{playOrder}"" playOrder=""{playOrder}"">
<navLabel><text>{EscapeXml(ch.Chapter.Title)}</text></navLabel>
<content src=""{ch.XhtmlFilename}""/>
</navPoint>");
        }
        return navMap.ToString();
    }

    private static string GetImageMediaType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }
}