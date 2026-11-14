using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// Public Google Drive folder listing + file download — no sign-in, no API
/// key. Same pattern as the Android app (ReportHelpScreen): the folder's
/// embedded-view page is fetched and its entries are parsed out of the HTML.
/// </summary>
public static class DriveListingService
{
    /// <summary>The user's public templates folder on Google Drive.</summary>
    public const string TemplatesFolderId = "1jwVlDTrLMytQvKYy3pKaQ6SUeEhxzTcP";

    public const string TemplatesFolderUrl = "https://drive.google.com/drive/folders/1jwVlDTrLMytQvKYy3pKaQ6SUeEhxzTcP";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    static DriveListingService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
    }

    public sealed record DriveEntry(string Id, string Name, bool IsFolder);

    // One entry looks like: id="entry-<id>" ... flip-entry-title">NAME<
    // and a folder's own <a href> points at /drive/folders/<id>.
    private static readonly Regex EntryRegex = new(
        "id=\"entry-([A-Za-z0-9_-]+)\".{0,2000}?flip-entry-title\">([^<]+)<",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Lists a public Drive folder. Empty list on any failure (the
    /// caller shows the error); never throws.</summary>
    public static async Task<List<DriveEntry>> FetchListingAsync(string folderId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var url = $"https://drive.google.com/embeddedfolderview?id={folderId}#list";
            using var res = await _http.GetAsync(url, cts.Token).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            var html = await res.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

            var list = new List<DriveEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in EntryRegex.Matches(html))
            {
                var id = m.Groups[1].Value;
                if (!seen.Add(id)) continue;
                var name = WebUtility.HtmlDecode(m.Groups[2].Value.Trim());
                if (string.IsNullOrWhiteSpace(name)) continue;
                var isFolder = m.Value.Contains($"/drive/folders/{id}", StringComparison.Ordinal);
                list.Add(new DriveEntry(id, name, isFolder));
            }
            return list;
        }
        catch
        {
            return new List<DriveEntry>();
        }
    }

    /// <summary>True when the folder page answers 200 (the folder exists
    /// and is public). A wrong id answers 404. Used to verify a folder id
    /// at first-time setup. Never throws.</summary>
    public static async Task<bool> CheckFolderReachableAsync(string folderId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var url = $"https://drive.google.com/embeddedfolderview?id={folderId}#list";
            using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Downloads a public Drive file into destDir under name.
    /// Returns false when the bytes are not a real .docx (a .docx is a ZIP
    /// starting with 'PK'; the Drive warning page is HTML) or on any error.
    /// Never throws.</summary>
    // تمپلیت‌های Word کوچک‌اند؛ بیشتر از این یعنی فایل اشتباهی یا دست‌کاری‌شده است
    private const long MaxDownloadBytes = 10L * 1024 * 1024;

    public static async Task<bool> DownloadFileAsync(string fileId, string name, string destDir)
    {
        try
        {
            // نام فایل باید یک نام ساده باشد؛ مسیر یا .. یعنی تلاش برای نوشتن بیرون از پوشه
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Contains("..") || name.IndexOfAny(new[] { '/', '\\' }) >= 0) return false;
            var safeName = Path.GetFileName(name);
            if (string.IsNullOrWhiteSpace(safeName) || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;

            Directory.CreateDirectory(destDir);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var url = $"https://drive.google.com/uc?export=download&id={fileId}";
            using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            // اگر سرور اندازه را اعلام کرد، از همان اول رد کن
            if (res.Content.Headers.ContentLength is { } declaredLength && declaredLength > MaxDownloadBytes) return false;

            // با سقف خواندن استریم، فایل بزرگ‌تر از ۱۰ مگابایت را وسط راه رد کن
            await using var stream = await res.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxDownloadBytes) return false;
                ms.Write(buffer, 0, read);
            }

            var bytes = ms.ToArray();
            if (bytes.Length < 100 || bytes[0] != (byte)'P' || bytes[1] != (byte)'K') return false;
            await File.WriteAllBytesAsync(Path.Combine(destDir, safeName), bytes, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
