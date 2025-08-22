using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SafariBooksDownloader.Core.Services;
using SafariBooksDownloader.Core.Utils;

namespace SafariBooksDownloader.App;

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
        var bookInfo = await client.GetBookInfoAsync(bookId);
        if (bookInfo is null)
        {
            Display.Exit("API: unable to retrieve book info.");
            return 1;
        }
        if (bookInfo.RootElement.ValueKind != JsonValueKind.Object)
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
        var metaInf = Path.Combine(bookPath, "META-INF");
        var oebpsPath = Path.Combine(bookPath, "OEBPS");
        var stylesPath = Path.Combine(oebpsPath, "Styles");
        var imagesPath = Path.Combine(oebpsPath, "Images");
        Directory.CreateDirectory(metaInf);
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
                Display.Error($"Failed to fetch chapter content: {ch.Title}");
                continue;
            }

            var (inlineCss, bodyXhtml) = htmlProcessor.ParseChapterHtml(htmlText, ch.AssetBaseUrl, bookId, ch, firstPage, allCss, allImages);
            var xhtmlContent = HtmlProcessor.BuildXhtml(opts.Kindle, inlineCss, bodyXhtml);

            var xhtmlFilename = $"Chapter{i + 1:000}.xhtml";
            var xhtmlPath = Path.Combine(oebpsPath, xhtmlFilename);
            await File.WriteAllTextAsync(xhtmlPath, xhtmlContent);

            processedChapters.Add(new ProcessedChapter { Chapter = ch, XhtmlFilename = xhtmlFilename });

            Display.State(chapters.Count, i + 1);
        }

        // 6) Download CSS files
        Display.Info($"Downloading stylesheets... ({allCss.Count} files)", state: true);
        for (int i = 0; i < allCss.Count; i++)
        {
            var cssContent = await client.GetStringAsync(allCss[i]);
            if (cssContent is not null)
            {
                var cssPath = Path.Combine(stylesPath, $"Style{i:00}.css");
                await File.WriteAllTextAsync(cssPath, cssContent);
            }
            Display.State(allCss.Count, i + 1);
        }

        // 7) Download images
        Display.Info($"Downloading images... ({allImages.Count} files)", state: true);
        for (int i = 0; i < allImages.Count; i++)
        {
            var imageBytes = await client.DownloadBytesAsync(allImages[i]);
            if (imageBytes is not null)
            {
                var imageName = Path.GetFileName(new Uri(allImages[i], UriKind.RelativeOrAbsolute).AbsolutePath);
                if (string.IsNullOrEmpty(imageName)) imageName = $"image{i}.jpg";
                var imagePath = Path.Combine(imagesPath, imageName);
                await File.WriteAllBytesAsync(imagePath, imageBytes);
            }
            Display.State(allImages.Count, i + 1);
        }

        // 8) Generate EPUB metadata
        Display.Info("Generating EPUB metadata...", state: true);

        var root = bookInfo.RootElement;
        var description = root.GetPropertyOrDefault("description", "");
        var authors = root.TryGetProperty("authors", out var authorsArray) && authorsArray.ValueKind == JsonValueKind.Array
            ? authorsArray.EnumerateArray().Select(a => a.GetPropertyOrDefault("name", "Unknown")).ToArray()
            : new[] { "Unknown" };
        var subjects = root.TryGetProperty("topics", out var topicsArray) && topicsArray.ValueKind == JsonValueKind.Array
            ? topicsArray.EnumerateArray().Select(t => t.GetPropertyOrDefault("name", "")).Where(s => !string.IsNullOrEmpty(s)).ToArray()
            : Array.Empty<string>();
        var publishers = root.TryGetProperty("publishers", out var pubArray) && pubArray.ValueKind == JsonValueKind.Array
            ? pubArray.EnumerateArray().Select(p => p.GetPropertyOrDefault("name", "")).Where(s => !string.IsNullOrEmpty(s)).ToArray()
            : new[] { "O'Reilly Media" };
        var rights = root.GetPropertyOrDefault("copyright", "All rights reserved");
        var issued = root.GetPropertyOrDefault("issued", DateTime.Now.ToString("yyyy-MM-dd"));

        var (manifest, spine, coverImageId) = EpubBuilder.BuildManifestAndSpine(processedChapters, allCss.Count, imagesPath);
        var contentOpf = EpubBuilder.BuildContentOpf(bookId, title, authors, description, subjects, publishers, rights, issued, coverImageId ?? "img_cover", manifest, spine, processedChapters.FirstOrDefault()?.XhtmlFilename ?? "Chapter001.xhtml");
        var navMap = EpubBuilder.BuildNavMapFromChapters(processedChapters);
        var tocNcx = EpubBuilder.BuildTocNcx(bookId, title, authors, navMap);

        await File.WriteAllTextAsync(Path.Combine(metaInf, "container.xml"), EpubBuilder.ContainerXml);
        await File.WriteAllTextAsync(Path.Combine(oebpsPath, "content.opf"), contentOpf);
        await File.WriteAllTextAsync(Path.Combine(oebpsPath, "toc.ncx"), tocNcx);

        // 9) Create EPUB
        Display.Info("Creating EPUB file...", state: true);
        var epubPath = Path.Combine(booksDir, $"{cleanTitle} ({bookId}).epub");
        if (File.Exists(epubPath)) File.Delete(epubPath);

        using (var zip = ZipFile.Open(epubPath, ZipArchiveMode.Create))
        {
            // 1. mimetype (must be first and uncompressed)
            var mimetypeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var s = mimetypeEntry.Open())
            {
                var bytes = Encoding.ASCII.GetBytes("application/epub+zip");
                s.Write(bytes, 0, bytes.Length);
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
        var handler = new SocketsHttpHandler()
        {
            CookieContainer = null,
            UseCookies = false,
        };

        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.UserAgent.Clear();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        http.DefaultRequestHeaders.Add("Cookie", cookieHeader);

        return http;
    }

    private static async Task AddFileToZip(ZipArchive zip, string filePath, string entryName)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = File.OpenRead(filePath);
        await fileStream.CopyToAsync(entryStream);
    }

    private static async Task AddDirectoryToZip(ZipArchive zip, string dir, string basePathInZip)
    {
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(dir, file).Replace('\\', '/');
            var entryName = $"{basePathInZip}/{relativePath}";
            await AddFileToZip(zip, file, entryName);
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
            var exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "SafariBooksDownloader";
            Console.WriteLine($"""
Usage: {exe} <book_id> [options]

Arguments:
  book_id                 The Safari/O'Reilly book ID (from URL)

Options:
  --kindle               Optimized for Kindle (adds page breaks, larger fonts)
  --preserve-log         Keep temporary log files after completion
  --help                 Show this help message

Examples:
  {exe} 9781449367480
  {exe} 9781449367480 --kindle
""");
        }
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