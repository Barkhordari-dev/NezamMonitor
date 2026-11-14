using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// Minimal, safe error log (ERR-001 contract).
/// Writes to %LocalAppData%/NezamMonitor/error.log.
/// Never throws; sensitive values (password / token / secret / ...)
/// are redacted before anything is written.
/// </summary>
public static class ErrorLog
{
    private static readonly object _lock = new();

    /// <summary>Maximum log size before rotation (1 MB).</summary>
    private const long MaxBytes = 1024 * 1024;

    // Longer keys first so "api_token" is matched as a whole, not as "token".
    private static readonly Regex RedactRegex = new(
        @"(?i)\b(api_token|password|passwd|pwd|token|secret|rekey)\b\s*[=:]\s*[^\s,;""']+",
        RegexOptions.Compiled);

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NezamMonitor", "error.log");

    public static void Write(Exception ex, string? context = null)
    {
        try
        {
            if (ex is null) return;

            var sb = new StringBuilder();
            sb.Append('[');
            sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"));
            sb.Append("] ");
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.Append('[');
                sb.Append(context);
                sb.Append("] ");
            }
            sb.Append(ex.GetType().FullName);
            sb.Append(": ");
            sb.AppendLine(ex.Message);

            var stack = ex.StackTrace;
            if (!string.IsNullOrWhiteSpace(stack))
            {
                var frames = stack.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var frame in frames.Take(5))
                {
                    sb.Append("    ");
                    sb.AppendLine(frame.Trim());
                }
            }

            var entry = Redact(sb.ToString());

            lock (_lock)
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                RotateIfNeeded();
                File.AppendAllText(LogPath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // ErrorLog must never throw, whatever happens.
        }
    }

    public static void Clear()
    {
        try
        {
            lock (_lock)
            {
                if (File.Exists(LogPath)) File.Delete(LogPath);
            }
        }
        catch
        {
            // Never throws.
        }
    }

    private static string Redact(string text)
    {
        try
        {
            // Keeps the key name, replaces only the value after '=' or ':'.
            return RedactRegex.Replace(text, m => m.Groups[1].Value + "=[REDACTED]");
        }
        catch
        {
            return "[unavailable]";
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(LogPath);
            if (!info.Exists || info.Length <= MaxBytes) return;

            var lines = File.ReadAllLines(LogPath);
            if (lines.Length == 0) return;

            // Drop the oldest half of the lines, keep the newest half.
            var keep = lines.Skip(lines.Length / 2).ToArray();
            File.WriteAllLines(LogPath, keep, Encoding.UTF8);
        }
        catch
        {
            // Rotation is best-effort; the log keeps working without it.
        }
    }
}
