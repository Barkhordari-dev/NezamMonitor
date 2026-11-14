using System.Runtime.InteropServices;

namespace NezamMonitorPro.Core.Reports;

/// <summary>
/// Converts a generated Word report to PDF through the installed Microsoft
/// Word (late-bound COM — no interop assembly, no new dependency).
/// One instance = one Word process, so a whole batch reuses it.
/// Every failure returns a short reason instead of throwing; the caller
/// keeps the Word file and reports the reason.
/// </summary>
public sealed class WordToPdfConverter : IDisposable
{
    private dynamic? _word;
    private dynamic? _docs;
    private bool _disposed;

    /// <summary>True when Word could be started.</summary>
    public bool IsAvailable => _word != null;

    /// <summary>Why Word could not be started (null when available).</summary>
    public string? Error { get; private set; }

    public static bool IsWordAvailable()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { return Type.GetTypeFromProgID("Word.Application") != null; }
        catch { return false; }
    }

    public WordToPdfConverter()
    {
        if (!OperatingSystem.IsWindows())
        {
            Error = "تبدیل PDF فقط در ویندوز با Word نصب‌شده ممکن است.";
            return;
        }
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            Error = "تبدیل PDF باید از نخ اصلی برنامه اجرا شود.";
            return;
        }
        try
        {
            var wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType == null)
            {
                Error = "برنامه Word روی این سیستم نصب نیست.";
                return;
            }
            dynamic? word = Activator.CreateInstance(wordType);
            if (word == null)
            {
                Error = "Word راه‌اندازی نشد.";
                return;
            }
            word.Visible = false;
            word.DisplayAlerts = 0; // wdAlertsNone — no dialogs while converting
            _word = word;
            _docs = word.Documents;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Dispose();
        }
    }

    /// <summary>Converts one Word file to PDF (next to it, same name).
    /// Returns true on success; on failure <paramref name="error"/> explains why.</summary>
    public bool Convert(string wordPath, string pdfPath, out string? error)
    {
        error = null;
        if (_word == null || _docs == null)
        {
            error = Error ?? "Word در دسترس نیست.";
            return false;
        }
        if (!File.Exists(wordPath))
        {
            error = "فایل Word یافت نشد.";
            return false;
        }
        dynamic? doc = null;
        try
        {
            if (_docs == null)
            {
                error = Error ?? "Word در دسترس نیست.";
                return false;
            }
            // ماکروها را پیش از باز کردن سند کاملا خاموش کن تا محتوای فعال
            // داخل فایل نتواند اجرا شود؛ اگر تنظیمش نشد، باز کردن سند ادامه پیدا می‌کند
            try { _word!.AutomationSecurity = 3; } catch { /* ادامه بده */ } // 3 = msoAutomationSecurityForceDisable

            doc = _docs.Open(wordPath);
            if (doc == null)
            {
                error = "سند Word باز نشد.";
                return false;
            }
            doc.ExportAsFixedFormat(pdfPath, 17); // 17 = wdExportFormatPDF
            doc.Close(false);
            doc = null;
            if (!File.Exists(pdfPath))
            {
                error = "فایل PDF ساخته نشد.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { if (doc != null) doc.Close(false); } catch { }
            try { if (doc != null) Marshal.ReleaseComObject(doc); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (_word != null) _word.Quit(); } catch { }
        try { if (_docs != null) Marshal.ReleaseComObject(_docs); } catch { }
        try { if (_word != null) Marshal.ReleaseComObject(_word); } catch { }
        _docs = null;
        _word = null;
    }
}
