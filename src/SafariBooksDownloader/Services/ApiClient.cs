using System.Text.Json;

namespace SafariBooksDownloader.Services;

internal sealed class ApiClient(HttpClient http)
{
    private static readonly Uri Base = new("https://learning.oreilly.com");

    public async Task<bool> CheckLoginAsync()
    {
        var resp = await GetAsync(new Uri(Base, "/profile/"));
        return resp?.StatusCode == System.Net.HttpStatusCode.OK
               && resp.Content != null;
    }

    public async Task<JsonDocument?> GetBookInfoAsync(string bookId)
    {
        var url = new Uri(Base, $"/api/v1/book/{bookId}/");
        var resp = await GetAsync(url);
        if (resp is null) return null;
        var text = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text);
    }

    public async Task<List<Chapter>> GetBookChaptersAsync(string bookId)
    {
        var all = new List<Chapter>();
        int page = 1;

        while (true)
        {
            var url = new Uri(Base, $"/api/v1/book/{bookId}/chapter/?page={page}");
            var resp = await GetAsync(url);
            if (resp is null) break;

            var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (json.RootElement.ValueKind != JsonValueKind.Object) break;

            var hasResults = json.RootElement.TryGetProperty("results", out var results) &&
                             results.ValueKind == JsonValueKind.Array;

            if (!hasResults || results.GetArrayLength() == 0) break;

            var temp = new List<Chapter>();
            foreach (var r in results.EnumerateArray())
            {
                var filename = r.GetPropertyOrDefault("filename", "chapter.xhtml");
                var title = r.GetPropertyOrDefault("title", "Chapter");
                var content = r.GetPropertyOrDefault("content", "");
                var assetBaseUrl = r.GetPropertyOrDefault("asset_base_url", "");
                var styles = r.TryGetProperty("stylesheets", out var st) && st.ValueKind == JsonValueKind.Array
                    ? st.EnumerateArray().Select(x => x.GetPropertyOrDefault("url", "")).Where(s => !string.IsNullOrEmpty(s)).ToList()
                    : new List<string>();
                if (r.TryGetProperty("site_styles", out var ss) && ss.ValueKind == JsonValueKind.Array)
                    styles.AddRange(ss.EnumerateArray().Select(x => x.ToString()).Where(s => !string.IsNullOrEmpty(s)));

                var images = new List<string>();
                if (r.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
                    images.AddRange(imgs.EnumerateArray().Select(x => x.ToString()).Where(s => !string.IsNullOrEmpty(s)));

                var ch = new Chapter
                {
                    Filename = filename,
                    Title = title,
                    Content = content,
                    AssetBaseUrl = assetBaseUrl,
                    Stylesheets = styles,
                    Images = images
                };
                temp.Add(ch);
            }

            var covers = temp.Where(c => c.Filename.Contains("cover", StringComparison.OrdinalIgnoreCase) ||
                                         c.Title.Contains("cover", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var c in covers) temp.Remove(c);
            all.AddRange(covers);
            all.AddRange(temp);

            var next = json.RootElement.TryGetProperty("next", out var nextProp) && nextProp.ValueKind != JsonValueKind.Null;
            if (!next) break;
            page++;
        }

        return all;
    }

    public async Task<string?> GetStringAsync(string url)
    {
        var resp = await GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
        return resp is null ? null : await resp.Content.ReadAsStringAsync();
    }

    public async Task<byte[]?> DownloadBytesAsync(string url, bool joinBase = false)
    {
        Uri uri = joinBase && !Uri.IsWellFormedUriString(url, UriKind.Absolute)
            ? new Uri(Base, url)
            : new Uri(url, UriKind.RelativeOrAbsolute);

        var resp = await GetAsync(uri, stream: true);
        if (resp is null) return null;
        return await resp.Content.ReadAsByteArrayAsync();
    }

    public async Task<HttpResponseMessage?> GetAsync(Uri url, bool stream = false)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            var resp = await http.SendAsync(req, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location is not null)
            {
                var next = resp.Headers.Location.IsAbsoluteUri ? resp.Headers.Location : new Uri(Base, resp.Headers.Location);
                return await GetAsync(next, stream);
            }
            return resp;
        }
        catch
        {
            return null;
        }
    }
}

internal static class JsonUtil
{
    public static string GetPropertyOrDefault(this JsonElement el, string name, string defaultValue)
        => el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : defaultValue;
}

internal sealed class Chapter
{
    public string Filename { get; set; } = "chapter.xhtml";
    public string Title { get; set; } = "Chapter";
    public string Content { get; set; } = "";
    public string AssetBaseUrl { get; set; } = "";
    public List<string> Stylesheets { get; set; } = new();
    public List<string> Images { get; set; } = new();
}

internal sealed class ProcessedChapter
{
    public Chapter Chapter { get; set; } = new();
    public string XhtmlFilename { get; set; } = "";
}