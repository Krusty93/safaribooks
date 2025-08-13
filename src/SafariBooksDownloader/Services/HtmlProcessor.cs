using HtmlAgilityPack;
using SafariBooksDownloader.Utils;

namespace SafariBooksDownloader.Services;

internal sealed class HtmlProcessor(ApiClient client)
{
    public (string InlineCss, string BodyXhtml) ParseChapterHtml(
        string htmlText,
        string baseUrl,
        string bookId,
        Chapter chapter,
        bool firstPage,
        List<string> globalCss,
        List<string> globalImages)
    {
        var doc = new HtmlDocument();
        doc.OptionFixNestedTags = true;
        doc.OptionOutputAsXml = true; // Ensure proper XHTML output with self-closing tags
        doc.LoadHtml(htmlText);

        foreach (var link in doc.DocumentNode.SelectNodes("//link[@rel='stylesheet']") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = link.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href)) continue;

            var abs = ToAbsolute(baseUrl, href);
            if (!globalCss.Contains(abs))
                globalCss.Add(abs);
        }

        var pageCssBuilder = new System.Text.StringBuilder();
        foreach (var style in doc.DocumentNode.SelectNodes("//style") ?? Enumerable.Empty<HtmlNode>())
        {
            var css = style.InnerHtml ?? "";
            pageCssBuilder.AppendLine($"<style>{css}</style>");
        }

        foreach (var s in chapter.Stylesheets)
        {
            if (!globalCss.Contains(s)) globalCss.Add(s);
        }

        var bodyNode = doc.DocumentNode.SelectSingleNode("//*[@id='sbo-rt-content']") 
                       ?? doc.DocumentNode.SelectSingleNode("//body") 
                       ?? doc.DocumentNode;

        foreach (var img in bodyNode.SelectNodes(".//img[@src]") ?? Enumerable.Empty<HtmlNode>())
        {
            var src = img.GetAttributeValue("src", "");
            if (string.IsNullOrWhiteSpace(src)) continue;

            var final = src.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? src
                : JoinUrl(chapter.AssetBaseUrl, src);

            if (!globalImages.Contains(final)) globalImages.Add(final);

            var filename = final.Split('/').Last();
            img.SetAttributeValue("src", $"Images/{filename}");
        }

        foreach (var a in bodyNode.SelectNodes(".//*[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href)) continue;

            if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                a.SetAttributeValue("href", href.Replace(".html", ".xhtml"));
            }
            else if (href.Contains(bookId))
            {
                var idx = href.IndexOf(bookId, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var rel = href[(idx + bookId.Length)..].TrimStart('/');
                    a.SetAttributeValue("href", rel.Replace(".html", ".xhtml"));
                }
            }
        }

        var bodyHtml = bodyNode.InnerHtml ?? "";

        for (int i = 0; i < globalCss.Count; i++)
        {
            pageCssBuilder.AppendLine($"<link href=\"Styles/Style{i:00}.css\" rel=\"stylesheet\" type=\"text/css\" />");
        }

        return (pageCssBuilder.ToString(), bodyHtml);
    }

    public static string BuildXhtml(bool kindle, string inlineCss, string bodyHtml)
    {
        var kindleCss = kindle
            ? "#sbo-rt-content *{word-wrap:break-word!important;word-break:break-word!important;}" +
              "#sbo-rt-content table,#sbo-rt-content pre{overflow-x:unset!important;overflow:unset!important;" +
              "overflow-y:unset!important;white-space:pre-wrap!important;}"
            : "";

        return $@"<!DOCTYPE html>
<html lang=""en"" xml:lang=""en"" xmlns=""http://www.w3.org/1999/xhtml""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
      xsi:schemaLocation=""http://www.w3.org/2002/06/xhtml2/ http://www.w3.org/MarkUp/SCHEMA/xhtml2.xsd""
      xmlns:epub=""http://www.idpf.org/2007/ops"">
<head>
{inlineCss}
<style type=""text/css"">
body{{margin:1em;background-color:transparent!important;}}
#sbo-rt-content *{{text-indent:0pt!important;}}
#sbo-rt-content .bq{{margin-right:1em!important;}}
{kindleCss}
</style>
</head>
<body>{bodyHtml}</body>
</html>";
    }

    private static string ToAbsolute(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
            return abs.ToString();

        var b = new Uri(baseUrl, UriKind.Absolute);
        return new Uri(b, href).ToString();
    }

    private static string JoinUrl(string baseUrl, string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return pathOrUrl;
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var abs)) return abs.ToString();
        var b = new Uri(baseUrl, UriKind.Absolute);
        return new Uri(b, pathOrUrl).ToString();
    }
}