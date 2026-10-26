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
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new NezamDatabase(DatabasePath);
                    }
                }
            }
            return _instance;
        }
    }
}
