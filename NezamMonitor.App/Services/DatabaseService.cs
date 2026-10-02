using System.IO;
using NezamMonitor.Core.Data;

namespace NezamMonitor.App.Services;

/// <summary>
/// Singleton service for database access.
/// Database is stored next to the executable for portability.
/// </summary>
public static class DatabaseService
{
    private static NezamDatabase? _instance;
    private static readonly object _lock = new();

    public static string DatabaseDirectory
    {
        get
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabasePath => Path.Combine(DatabaseDirectory, "nezam_monitor.db");

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
