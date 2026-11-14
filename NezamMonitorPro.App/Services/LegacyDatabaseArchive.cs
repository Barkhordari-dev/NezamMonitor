using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace NezamMonitorPro.App.Services;

/// <summary>یک پایگاه‌داده قدیمی (نسخه‌های قبلی برنامه) که به‌صورت بایگانی و فقط‌خواندنی افزوده شده است.</summary>
public sealed class LegacyArchiveItem
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public long SizeBytes { get; init; }
    public DateTime AddedAt { get; init; }
    public string Summary { get; init; } = "";
    public bool IsReadable { get; init; }
    public string CaseCountText { get; init; } = "";

    public string SizeText => SizeBytes >= 1024 * 1024
        ? string.Format(CultureInfo.InvariantCulture, "{0:n1} MB", SizeBytes / 1024d / 1024d)
        : string.Format(CultureInfo.InvariantCulture, "{0:n0} KB", SizeBytes / 1024d);

    public string AddedAtText => AddedAt.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>
/// بایگانی پایگاه‌داده‌های نسخه‌های قبلی.
/// ⚠ این کلاس هرگز پایگاه‌داده فعال برنامه را جایگزین یا تغییر نمی‌کند؛ فقط یک کپی
/// از فایل قدیمی را در data\legacy نگه می‌دارد و آن را به‌صورت ReadOnly می‌خواند.
/// </summary>
public static class LegacyDatabaseArchive
{
    private static readonly string[] KnownTables =
    {
        "Cases", "Engineers", "Snapshots", "Reports", "Fees", "FollowUps"
    };

    public static string Folder
    {
        get
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "legacy");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>فهرست پایگاه‌داده‌های بایگانی‌شده (بدون دست‌زدن به پایگاه‌داده فعال).</summary>
    public static List<LegacyArchiveItem> List()
    {
        var items = new List<LegacyArchiveItem>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(Folder, "*.db")
                                              .OrderByDescending(f => File.GetLastWriteTime(f)))
            {
                var info = new FileInfo(file);
                var (readable, summary, cases) = Probe(file);
                items.Add(new LegacyArchiveItem
                {
                    Name = info.Name,
                    Path = info.FullName,
                    SizeBytes = info.Length,
                    AddedAt = info.LastWriteTime,
                    Summary = summary,
                    IsReadable = readable,
                    CaseCountText = cases
                });
            }
        }
        catch
        {
            // بایگانی نباید هیچ‌گاه برنامه را متوقف کند
        }

        return items;
    }

    /// <summary>
    /// یک فایل پایگاه‌داده قدیمی را کپی و بایگانی می‌کند. فایل مبدا دست‌نخورده می‌ماند
    /// و پایگاه‌داده فعال برنامه هرگز تغییر نمی‌کند.
    /// </summary>
    public static LegacyArchiveItem Attach(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("فایل پایگاه‌داده پیدا نشد.", sourcePath);

        var baseName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        foreach (var bad in System.IO.Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(bad, '_');

        var target = System.IO.Path.Combine(
            Folder,
            $"{DateTime.Now:yyyyMMdd_HHmmss}_{baseName}.db");

        File.Copy(sourcePath, target, overwrite: false);

        var info = new FileInfo(target);
        var (readable, summary, cases) = Probe(target);

        return new LegacyArchiveItem
        {
            Name = info.Name,
            Path = info.FullName,
            SizeBytes = info.Length,
            AddedAt = info.LastWriteTime,
            Summary = summary,
            IsReadable = readable,
            CaseCountText = cases
        };
    }

    /// <summary>حذف یک مورد از بایگانی (فقط کپی بایگانی‌شده؛ فایل اصلی کاربر دست‌نخورده است).</summary>
    public static void Remove(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // ignore: file may be locked
        }
    }

    /// <summary>
    /// پایگاه‌داده‌هایی که کاربر کنار فایل اجرایی یا در پوشه data گذاشته است
    /// (مثل فایل قدیمی nezam_monitor.db در ریشه برنامه). فقط شناسایی می‌شوند؛
    /// هیچ تغییری روی آن‌ها اعمال نمی‌شود و پایگاه‌داده فعال نادیده گرفته می‌شود.
    /// </summary>
    public static List<LegacyArchiveItem> ScanCandidates()
    {
        var result = new List<LegacyArchiveItem>();

        try
        {
            var appDir = AppContext.BaseDirectory;
            var activeDb = System.IO.Path.GetFullPath(System.IO.Path.Combine(appDir, "data", "nezam_monitor.db"));
            var legacyDir = System.IO.Path.GetFullPath(Folder);

            var roots = new[]
            {
                appDir,
                System.IO.Path.Combine(appDir, "data"),
                System.IO.Path.Combine(appDir, "backup"),
                System.IO.Path.Combine(appDir, "backups")
            };

            foreach (var dir in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.EnumerateFiles(dir, "*.db"))
                {
                    var full = System.IO.Path.GetFullPath(file);
                    if (string.Equals(full, activeDb, StringComparison.OrdinalIgnoreCase)) continue;
                    if (full.StartsWith(legacyDir + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;

                    var info = new FileInfo(full);
                    var (readable, summary, cases) = Probe(full);
                    result.Add(new LegacyArchiveItem
                    {
                        Name = info.Name,
                        Path = info.FullName,
                        SizeBytes = info.Length,
                        AddedAt = info.LastWriteTime,
                        Summary = summary,
                        IsReadable = readable,
                        CaseCountText = cases
                    });
                }
            }
        }
        catch
        {
            // اسکن نباید هیچ‌گاه برنامه را متوقف کند
        }

        return result;
    }

    /// <summary>تلاش برای خواندن ساختار فایل، بدون هیچ تغییری در آن.</summary>
    private static (bool Readable, string Summary, string CaseCount) Probe(string path)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private
            };

            using var conn = new SqliteConnection(builder.ToString());
            conn.Open();

            var tables = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) tables.Add(reader.GetString(0));
            }

            if (tables.Count == 0)
                return (true, "جدولی یافت نشد", "—");

            long CountOf(string table)
            {
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
                    return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
                }
                catch { return 0; }
            }

            var known = KnownTables
                .Where(t => tables.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var casesTable = known.FirstOrDefault(t => string.Equals(t, "Cases", StringComparison.OrdinalIgnoreCase));
            var cases = casesTable is null ? "—" : CountOf(casesTable).ToString("n0", CultureInfo.InvariantCulture);

            var summary = known.Count == 0
                ? $"{tables.Count} جدول"
                : string.Join(" · ", known.Select(t => $"{t}: {CountOf(t):n0}"));

            return (true, summary, cases);
        }
        catch
        {
            return (false, "قابل خواندن نیست (احتمالا رمزنگاری‌شده یا نسخه قدیمی SQLite)", "—");
        }
    }
}
