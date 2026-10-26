using System.IO;

namespace NezamMonitorPro.App.Services;

/// <summary>Simple frame-based navigation using dictionary of view factories.</summary>
public sealed class NavigationService
{
    private readonly Dictionary<string, Func<System.Windows.Controls.UserControl>> _factories = new();

    /// <summary>
    /// هر ویو فقط یک بار ساخته می‌شود و بعد از آن دوباره استفاده می‌شود.
    /// ساخت دوبارهٔ بعضی ویوها (مثل UpdateView) به کرش قطعی می‌انجامید.
    /// </summary>
    private readonly Dictionary<string, System.Windows.Controls.UserControl> _views = new();

    /// <summary>
    /// فقط این دو ویو کش می‌شوند (ساخت دوباره‌شان مشکل‌ساز بود)؛ بقیهٔ صفحات
    /// مثل قبل هر بار نو ساخته می‌شوند تا داده‌شان همیشه تازه باشد.
    /// </summary>
    private readonly HashSet<string> _cacheable = new(StringComparer.OrdinalIgnoreCase) { "update", "history" };

    private System.Windows.Controls.ContentControl? _frame;
    private string _currentKey = "";

    public event Action<string>? Navigated;
    public string CurrentKey => _currentKey;

    public void Register(string key, Func<System.Windows.Controls.UserControl> factory)
        => _factories[key] = factory;

    public void SetFrame(System.Windows.Controls.ContentControl frame)
        => _frame = frame;

    public void NavigateTo(string key)
    {
        if (_frame is null) return;
        if (!_factories.TryGetValue(key, out var factory))
        {
            Log($"NAVIGATE '{key}': no factory registered");
            return;
        }

        try
        {
            var cacheable = _cacheable.Contains(key);
            if (!cacheable || !_views.TryGetValue(key, out var view) || view is null)
            {
                Log($"NAVIGATE '{key}': creating view ...");
                view = factory();
                if (cacheable) _views[key] = view;
                Log($"NAVIGATE '{key}': view created ({view?.GetType().Name})");

                // DataContext باید پیش از نمایش ست شده باشد
                if (view is not null && view.DataContext is null)
                    Log($"NAVIGATE '{key}': WARNING DataContext is null");
            }

            Log($"NAVIGATE '{key}': setting content ...");
            _frame.Content = view;
            _currentKey = key;

            // ویوهای کش‌شده اینجا داده‌شان را تازه می‌کنند
            if (view is INavigationAware aware)
            {
                try { aware.OnNavigatedTo(); }
                catch (Exception ex) { Log($"NAVIGATE '{key}': OnNavigatedTo failed: {ex}"); }
            }

            Log($"NAVIGATE '{key}': raising Navigated ...");
            Navigated?.Invoke(key);
            Log($"NAVIGATE '{key}': done");
        }
        catch (Exception ex)
        {
            Log($"NAVIGATION FAILED for '{key}': {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            _currentKey = key;

            // به‌جای کرش، یک پیام قابل‌فهم به کاربر نشان بده
            try
            {
                System.Windows.MessageBox.Show(
                    $"باز کردن صفحهٔ «{key}» ممکن نشد:\n{ex.Message}\n\nجزئیات در startup.log ثبت شد.",
                    "خطا در باز کردن صفحه",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            catch { }
        }
    }

    /// <summary>Writes navigation diagnostics to startup.log next to the executable.</summary>
    private static void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                      ?? AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(dir, "startup.log");
            File.AppendAllText(path, $"[{DateTime.Now}] {message}\n");
        }
        catch { }
    }

    public bool CanNavigate(string key) => _factories.ContainsKey(key);
}
