using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SafariBooksDownloader.Services;
using SafariBooksDownloader.Utils;

namespace SafariBooksDownloader;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var opts = CliOptions.Parse(args);
        if (!opts.Valid || opts.Help)
        {
            CliOptions.PrintHelp();
            return opts.Help ? 0 : 1;
        }

        var exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var cookiesPath = Path.Combine(exeDir, "cookies.json");
        if (!File.Exists(cookiesPath))
        {
            Display.Error("Login: unable to find `cookies.json` file.\n    Please mount or place cookies.json next to the executable.");
            return 1;
        }

        Dictionary<string, string>? cookies;
        try
        {
            cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(cookiesPath));
        }
        catch (Exception ex)
        {
            Display.Error("Invalid cookies.json: " + ex.Message);
            return 1;
        }

        if (cookies == null || cookies.Count == 0)
        {
            Display.Error("cookies.json is empty.");
            return 1;
        }

        var cookieHeader = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
        using var http = BuildHttp(cookieHeader);

        var client = new ApiClient(http);

        // 1) Verify authentication
        Display.Info("Checking authentication...", state: true);
        if (!await client.CheckLoginAsync())
        {
            Display.Exit("Authentication issue: unable to access profile page. Cookies likely expired.");
            return 1;
        }
        Display.Info("Successfully authenticated.", state: true);

        // 2) Fetch book info
        var bookId = opts.BookId!;
        Display.Info("Retrieving book info...");
        var bookInfo = await client.GetBookInfoAsync(bookId) 
                        ?? Display.ExitReturn<JsonDocument?>("API: unable to retrieve book info.");
        if (bookInfo!.RootElement.ValueKind != JsonValueKind.Object)
        {
            Display.Exit("API: unexpected response for book info.");
            return 1;
        }

        var title = bookInfo.RootElement.GetPropertyOrDefault("title", "n/a");
        var cleanTitle = PathUtils.CleanFileName(string.Join("", title.Split(',').Take(2)));
        var booksDir = Path.Combine(exeDir, "Books");
        Directory.CreateDirectory(booksDir);

        var bookDirName = $"{cleanTitle} ({bookId})";
        var bookPath = Path.Combine(booksDir, bookDirName);
        Directory.CreateDirectory(bookPath);

        Display.SetOutputDir(bookPath);

        // 3) Fetch chapters (with pagination)
        Display.Info("Retrieving book chapters...");
        var chapters = await client.GetBookChaptersAsync(bookId);
        if (chapters.Count == 0)
        {
            Display.Exit("API: unable to retrieve book chapters.");
            return 1;
        }

        // 4) Prepare folders
        var oebpsPath = Path.Combine(bookPath, "OEBPS");
        var stylesPath = Path.Combine(oebpsPath, "Styles");
        var imagesPath = Path.Combine(oebpsPath, "Images");
        Directory.CreateDirectory(oebpsPath);
        Directory.CreateDirectory(stylesPath);
        Directory.CreateDirectory(imagesPath);

        var htmlProcessor = new HtmlProcessor(client);

        // 5) Download content
        var allCss = new List<string>();
        var allImages = new List<string>();
        var processedChapters = new List<ProcessedChapter>();

        Display.Info($"Downloading book contents... ({chapters.Count} chapters)", state: true);

        for (int i = 0; i < chapters.Count; i++)
        {
            var ch = chapters[i];
            var firstPage = (i == 0);

            var htmlText = await client.GetStringAsync(ch.Content);
            if (htmlText is null)
            {
                Display.Exit($"Crawler: error trying to retrieve this page: {ch.Filename} ({ch.Title})\n    From: {ch.Content}");
                return 1;
            }

            var result = htmlProcessor.ParseChapterHtml(
                htmlText,
                baseUrl: bookInfo.RootElement.GetPropertyOrDefault("web_url", "https://learning.oreilly.com"),
                bookId: bookId,
                chapter: ch,
                firstPage: firstPage,
                globalCss: allCss,
                globalImages: allImages
            );

            // Save chapter XHTML
            var xhtmlFilename = ch.Filename.Replace(".html", ".xhtml");
            await File.WriteAllTextAsync(
                Path.Combine(oebpsPath, xhtmlFilename),
                HtmlProcessor.BuildXhtml(opts.Kindle, result.InlineCss, result.BodyXhtml),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            processedChapters.Add(new ProcessedChapter
            {
                Chapter = ch,
                XhtmlFilename = xhtmlFilename
            });
            Display.State(chapters.Count, i + 1);
        }

        // 6) Download CSS
        Display.Info($"Downloading book CSSs... ({allCss.Count} files)", state: true);
        for (int i = 0; i < allCss.Count; i++)
        {
            var url = allCss[i];
            var cssFile = Path.Combine(stylesPath, $"Style{i:00}.css");
            if (File.Exists(cssFile)) continue;

            var cssBytes = await client.DownloadBytesAsync(url);
            if (cssBytes is null) continue;

            await File.WriteAllBytesAsync(cssFile, cssBytes);
            Display.State(allCss.Count, i + 1);
        }

        // 7) Download images
        Display.Info($"Downloading book images... ({allImages.Count} files)", state: true);
        for (int i = 0; i < allImages.Count; i++)
        {
            var url = allImages[i];
            var imageName = url.Split('/').LastOrDefault() ?? $"img{i}";
            var imagePath = Path.Combine(imagesPath, imageName);

            if (File.Exists(imagePath)) { Display.State(allImages.Count, i + 1); continue; }

            var imgBytes = await client.DownloadBytesAsync(url, joinBase: true);
            if (imgBytes is null) continue;

            await File.WriteAllBytesAsync(imagePath, imgBytes);
            Display.State(allImages.Count, i + 1);
        }

        // 8) Build EPUB (mimetype, META-INF/container.xml, OEBPS/content.opf, OEBPS/toc.ncx)
        Display.Info("Creating EPUB file...", state: true);
        var epubPath = Path.Combine(bookPath, $"{bookId}.epub");

        // Write mimetype and META-INF/container.xml and OEBPS/*.opf/ncx (as files)
        var metaInf = Path.Combine(bookPath, "META-INF");
        Directory.CreateDirectory(metaInf);
        await File.WriteAllTextAsync(Path.Combine(bookPath, "mimetype"), "application/epub+zip", Encoding.ASCII);
        await File.WriteAllTextAsync(Path.Combine(metaInf, "container.xml"), EpubBuilder.ContainerXml, new UTF8Encoding(false));

        // Build OPF and NCX content
        var manifestAndSpine = EpubBuilder.BuildManifestAndSpine(processedChapters, stylesCount: allCss.Count, imagesPath);
        var contentOpf = EpubBuilder.BuildContentOpf(
            id: bookInfo.RootElement.GetPropertyOrDefault("isbn", bookId),
            title: title,
            authors: bookInfo.RootElement.GetArrayStrings("authors", "name"),
            description: bookInfo.RootElement.GetStringOrEmpty("description"),
            subjects: bookInfo.RootElement.GetArrayStrings("subjects", "name"),
            publishers: bookInfo.RootElement.GetArrayStrings("publishers", "name"),
            rights: bookInfo.RootElement.GetPropertyOrDefault("rights", ""),
            issued: bookInfo.RootElement.GetPropertyOrDefault("issued", ""),
            coverItemId: "cover",
            manifest: manifestAndSpine.Manifest,
            spine: manifestAndSpine.Spine,
            coverHref: processedChapters.First().XhtmlFilename
        );
        await File.WriteAllTextAsync(Path.Combine(oebpsPath, "content.opf"), contentOpf, new UTF8Encoding(false));

        var tocNcx = EpubBuilder.BuildTocNcx(
            id: bookInfo.RootElement.GetPropertyOrDefault("isbn", bookId),
            title: title,
            authors: bookInfo.RootElement.GetArrayStrings("authors", "name"),
            navMap: EpubBuilder.BuildNavMapFromChapters(processedChapters)
        );
        await File.WriteAllTextAsync(Path.Combine(oebpsPath, "toc.ncx"), tocNcx, new UTF8Encoding(false));

        // Zip into EPUB ensuring mimetype is first and uncompressed
        if (File.Exists(epubPath)) File.Delete(epubPath);
        using (var fs = new FileStream(epubPath, FileMode.CreateNew))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: Encoding.UTF8))
        {
            // 1. mimetype (must be first, stored without compression)
            var mimetypeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            await using (var s = mimetypeEntry.Open())
            {
                var bytes = Encoding.ASCII.GetBytes("application/epub+zip");
                await s.WriteAsync(bytes);
            }

            // 2. META-INF/container.xml
            await AddFileToZip(zip, Path.Combine(metaInf, "container.xml"), "META-INF/container.xml");

            // 3. OEBPS/*
            await AddDirectoryToZip(zip, oebpsPath, "OEBPS");
        }

        if (!opts.PreserveLog)
        {
            // No extra logs to clean up in this rewrite.
        }

        Display.Done(epubPath);
        return 0;
    }

    private static HttpClient BuildHttp(string cookieHeader)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(100)
        };

        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        http.DefaultRequestHeaders.Referrer = new Uri("https://learning.oreilly.com/login/unified/?next=/home/");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124 Safari/537.36");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
        return http;
    }

    private static async Task AddFileToZip(ZipArchive zip, string filePath, string entryName)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var s = entry.Open();
        await using var f = File.OpenRead(filePath);
        await f.CopyToAsync(s);
    }

    private static async Task AddDirectoryToZip(ZipArchive zip, string dir, string basePathInZip)
    {
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(dir, file).Replace('\\', '/');
            await AddFileToZip(zip, file, $"{basePathInZip}/{relPath}");
        }
    }

    private sealed class CliOptions
    {
        public bool Valid { get; private set; }
        public bool Help { get; private set; }
        public bool Kindle { get; private set; }
        public bool PreserveLog { get; private set; }
        public string? BookId { get; private set; }

        public static CliOptions Parse(string[] args)
        {
            var o = new CliOptions();
            if (args.Length == 0 || args.Contains("--help"))
            {
                o.Help = true;
                o.Valid = false;
                return o;
            }

            foreach (var a in args)
            {
                if (a == "--kindle") o.Kindle = true;
                else if (a == "--preserve-log") o.PreserveLog = true;
                else if (!a.StartsWith("-") && o.BookId is null) o.BookId = a;
            }

            o.Valid = o.BookId is not null;
            return o;
        }

        public static void PrintHelp()
        {
            Console.WriteLine(@"usage: SafariBooksDownloader [--kindle] [--preserve-log] [--help] <BOOK ID>

Download and generate an EPUB of your favorite books from O'Reilly Learning (Safari Books Online) using cookie-based auth.

positional arguments:
  <BOOK ID>            Book digits ID found in the URL:
                       https://learning.oreilly.com/library/view/BOOKNAME/XXXXXXXXXXXXX/

optional arguments:
  --kindle             Add CSS rules that improve Kindle rendering (tables, pre).
  --preserve-log       Keep logs (no-op placeholder).
  --help               Show this help message.");
        }
    }
}

static class JsonExtensions
{
    public static string GetPropertyOrDefault(this JsonElement el, string name, string defaultValue)
        => el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : defaultValue;

    public static string GetStringOrEmpty(this JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : string.Empty;

    public static string[] GetArrayStrings(this JsonElement el, string name, string innerProp)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(innerProp, out var v))
                list.Add(v.ToString());
        }
        return list.ToArray();
    }
}

static class Display
{
    private static string _outputDir = "";

    public static void SetOutputDir(string dir)
    {
        _outputDir = dir;
        Info("Output directory:\n    " + dir);
    }

    public static void Info(string message, bool state = false)
    {
        Console.WriteLine((state ? "[-]" : "[*]") + " " + message);
    }

    public static void Error(string message)
    {
        Console.WriteLine("[#] " + message);
    }

    public static void Exit(string message)
    {
        Error(message);
        if (!string.IsNullOrEmpty(_outputDir))
        {
            Console.WriteLine("[+] Please delete the output directory '" + _outputDir + "' and restart the program.");
        }
        Console.WriteLine("[!] Aborting...");
    }

    public static T? ExitReturn<T>(string message) where T : class
    {
        Exit(message);
        return null;
    }

    public static void Done(string epubFile)
    {
        Info("Done: " + epubFile + "\n\n" +
             "    If you like it, please star the project on GitHub.\n" +
             "    And don't forget to renew your O'Reilly subscription.\n\n" +
             "[!] Bye!!");
    }

    public static void State(int origin, int done)
    {
        var progress = Math.Max(0, Math.Min(100, (int)(done * 100.0 / origin)));
        Console.WriteLine($"    [{new string('#', progress / 2).PadRight(50, '-')}] {progress,4}%");
    }
}