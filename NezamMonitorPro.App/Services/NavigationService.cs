using System.IO;

namespace NezamMonitorPro.App.Services;

/// <summary>Simple frame-based navigation using dictionary of view factories.</summary>
public sealed class NavigationService
{
    private readonly Dictionary<string, Func<System.Windows.Controls.UserControl>> _factories = new();

    /// <summary>
    /// هر ویو فقط یک بار ساخته می‌شود و بعد از آن دوباره استفاده می‌شود.
    /// ساخت دوباره بعضی ویوها (مثل UpdateView) به کرش قطعی می‌انجامید.
    /// </summary>
    private readonly Dictionary<string, System.Windows.Controls.UserControl> _views = new();

    /// <summary>
    /// این ویوها کش می‌شوند: update/history چون ساخت دوباره‌شان مشکل‌ساز بود،
    /// و cases چون ساخت دوباره آن (پارس XAML + لود داده + چیدمان جدول ۳۵
    /// ستونه) هر بار ورود به منوی پرونده‌ها را کند می‌کرد. تازگی داده
    /// پرونده‌ها با بررسی شناسه اسنپ‌شات فعال در OnNavigatedTo تضمین می‌شود:
    /// اگر داده جدیدی استخراج شده باشد، همان‌جا دوباره لود می‌شود؛ وگرنه
    /// ویوی آماده نمایش داده می‌شود. بقیه صفحات مثل قبل هر بار نو ساخته
    /// می‌شوند تا داده‌شان همیشه تازه باشد.
    /// </summary>
    private readonly HashSet<string> _cacheable = new(StringComparer.OrdinalIgnoreCase) { "update", "history", "cases", "generator" };

    private System.Windows.Controls.ContentControl? _frame;
    private string _currentKey = "";

    /// <summary>True while the first Cases load is showing its loading
    /// placeholder (prevents the deferred creation from re-showing it).</summary>
    private bool _casesLoadingShown;

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
            // B-016: an unknown key (e.g. from a deep link) used to be
            // ignored silently. Log it so it can be diagnosed; the app
            // stays on the current page instead of crashing.
            ErrorLog.Write(
                new InvalidOperationException(
                    $"Unknown navigation key '{key}'. Registered keys: {string.Join(", ", _factories.Keys)}"),
                "NavigationService.NavigateTo");
            return;
        }

        try
        {
            var cacheable = _cacheable.Contains(key);
            if (!cacheable || !_views.TryGetValue(key, out var view) || view is null)
            {
                // First-ever Cases navigation: the view is heavy to build
                // (XAML parse + ViewModel load + first layout of the
                // 35-column grid), so show a friendly loading page first and
                // build the real view right after it has painted. Later
                // visits show the cached view instantly and skip this.
                if (key.Equals("cases", StringComparison.OrdinalIgnoreCase) && !_casesLoadingShown)
                {
                    _casesLoadingShown = true;
                    try
                    {
                        _frame.Content = new NezamMonitorPro.App.Views.CasesLoadingView();
                        _currentKey = key;
                        Navigated?.Invoke(key);
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(() =>
                            {
                                try
                                {
                                    // The user may have navigated away while
                                    // the placeholder was showing; then don't
                                    // yank them back — the next visit will
                                    // show the placeholder again.
                                    if (_currentKey != key) { _casesLoadingShown = false; return; }
                                    NavigateTo(key);
                                }
                                catch { }
                            }));
                        return;
                    }
                    catch
                    {
                        // Loading placeholder unavailable: fall through and
                        // build the real view directly (the flag stays set
                        // so we don't retry the placeholder).
                    }
                }

                view = factory();
                if (cacheable) _views[key] = view;
            }

            _frame.Content = view;
            _currentKey = key;

            // ویوهای کش‌شده اینجا داده‌شان را تازه می‌کنند
            if (view is INavigationAware aware)
            {
                try { aware.OnNavigatedTo(); }
                catch { }
            }

            Navigated?.Invoke(key);
        }
        catch (Exception ex)
        {
            _currentKey = key;

            // به‌جای کرش، یک پیام قابل‌فهم به کاربر نشان بده
            try
            {
                System.Windows.MessageBox.Show(
                    $"باز کردن صفحه «{key}» ممکن نشد:\n{ex.Message}",
                    "خطا در باز کردن صفحه",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            catch { }
        }
    }


    public bool CanNavigate(string key) => _factories.ContainsKey(key);
}
