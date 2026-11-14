using Microsoft.Data.Sqlite;
using System.IO;
using System.Diagnostics;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// Singleton service for database access.
/// Database is stored next to the executable for portability.
/// </summary>
public static class DatabaseService
{
    private static NezamDatabase? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Returns the directory where the executable file resides, works correctly for single-file bundles.
    /// </summary>
    public static string GetAppDirectory()
    {
        // In single-file bundles, Process.GetCurrentProcess().MainModule.FileName points to the original exe location.
        return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    public static string DatabaseDirectory
    {
        get
        {
            var dir = Path.Combine(GetAppDirectory(), "data");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabasePath => Path.Combine(DatabaseDirectory, "nezam_monitor.db");

    
    public static void Initialize(SqliteConnection connection)
    {
        lock (_lock)
        {
            _instance = new NezamDatabase(connection);
        }
    }

    public static NezamDatabase Instance
    {
        get
        {
            // اگر هنوز Initialize صدا نشده، هرگز اتصال plain نساز؛
            // صدا زدن Instance پیش از ورود یعنی باگ در ترتیب راه‌اندازی است
            if (_instance == null)
                throw new InvalidOperationException("پایگاه‌داده هنوز باز نشده است؛ اول باید ورود انجام شود.");
            return _instance;
        }
    }

    /// <summary>
    /// اتصال singleton را می‌بندد تا بعد از ریستور بکاپ، برنامه دوباره با
    /// فایل تازه بازش کند. (Dispose اتصال را می‌بندد.)
    /// </summary>
    public static void Close()
    {
        lock (_lock)
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}
