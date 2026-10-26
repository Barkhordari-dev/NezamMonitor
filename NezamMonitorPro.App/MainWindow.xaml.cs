using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly NavigationService _nav;
    private readonly ThemeManager _themeManager;

    /// <summary>Nav metadata: key → (title, subtitle).</summary>
    private static readonly Dictionary<string, (string Title, string Subtitle)> PageMeta = new()
    {
        ["dashboard"]     = ("داشبورد", "نمای کلی وضعیت پرونده‌ها"),
        ["update"]        = ("به‌روزرسانی", "دریافت آخرین اطلاعات از سامانه"),
        ["generator"]     = ("تولید گزارش", "ساخت گزارش مرحله‌ای Word از روی قالب"),
        ["cases"]         = ("پرونده‌ها", "مدیریت و جستجوی پرونده‌های مهندسی"),
        ["reports"]       = ("گزارش‌ها", "آرشیو گزارش‌های تولیدشده"),
        ["excel"]         = ("خروجی اکسل", "دریافت خروجی اکسل از سامانه"),
        ["androidexport"] = ("خروجی اندروید", "ساخت فایل برای اپلیکیشن موبایل"),
        ["engineers"]     = ("مهندسین", "فهرست ناظرین و وضعیت مشارکت"),
        ["fees"]          = ("حق‌الزحمه", "محاسبه و مدیریت حق‌الزحمه‌ها"),
        ["followup"]      = ("پیگیری", "پرونده‌های نیازمند پیگیری"),
        ["changes"]       = ("تغییرات", "مقایسه با اسکن قبلی"),
        ["history"]       = ("تاریخچه", "سابقه اسکن‌ها و عملیات"),
        ["settings"]      = ("تنظیمات", "تم، فونت، مسیرها و پشتیبان‌گیری"),
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _nav = App.Navigation;
        _nav.SetFrame(MainFrame);

        _themeManager = new ThemeManager(DatabaseService.Instance);
        _themeManager.LoadTheme();
        _themeManager.ApplyTheme(_themeManager.CurrentTheme);

        _nav.Register("dashboard", () => new Views.DashboardView());
        _nav.Register("cases", () => new Views.CasesView());
        _nav.Register("update", () => new Views.UpdateView());
        _nav.Register("changes", () => new Views.ChangesView());
        _nav.Register("engineers", () => new Views.EngineersView());
        _nav.Register("fees", () => new Views.FeesView());
        _nav.Register("reports", () => new Views.ReportsView());
        _nav.Register("generator", () => new Views.GeneratorView());
        _nav.Register("excel", () => new Views.ExcelExportView());
        _nav.Register("history", () => new Views.HistoryView());
        _nav.Register("followup", () => new Views.FollowUpView());
        _nav.Register("androidexport", () => new Views.AndroidExportView());
        _nav.Register("settings", () => new Views.SettingsView());

        _nav.Navigated += OnNavigated;
        _nav.NavigateTo("dashboard");
        UpdatePageHeader("dashboard");

        // عکس‌برداری از ابعاد واقعی چیدمان در startup.log (برای عیب‌یابی نوار سفید)
        EnsureContentColumnStar();
        Loaded += (_, _) =>
        {
            EnsureContentColumnStar();
            Dispatcher.BeginInvoke(new Action(() => LogLayout("loaded")),
                System.Windows.Threading.DispatcherPriority.ContextIdle);
        };
    }

    /// <summary>
    /// ستون ۱ (محتوای اصلی) باید همیشه * باشد. بعضی مسیرها (مثلاً اعمال تم از صفحهٔ تنظیمات)
    /// عرض ستون‌ها را بازنویسی می‌کردند و محتوا به ۲۶۴ پیکسل مچاله می‌شد.
    /// </summary>
    private void EnsureContentColumnStar()
    {
        try
        {
            if (Content is not Grid grid || grid.ColumnDefinitions.Count < 2) return;

            var sidebar = grid.ColumnDefinitions[0];
            if (!sidebar.Width.IsStar && sidebar.Width.Value < 180)
                sidebar.Width = new GridLength(264);

            var content = grid.ColumnDefinitions[1];
            if (!content.Width.IsStar)
                content.Width = new GridLength(1, GridUnitType.Star);
        }
        catch { }
    }

    /// <summary>
    /// ابعاد واقعی کنترل‌های اصلی را در startup.log می‌نویسد تا اگر چیدمان درست نبود،
    /// دقیقاً مشخص شود کدام سطح فضای موجود را پر نکرده است.
    /// </summary>
    private void LogLayout(string stage)
    {
        try
        {
            var content = MainFrame?.Content as FrameworkElement;
            var sb = new System.Text.StringBuilder();
            double dpiX = 1, dpiY = 1;
            try
            {
                var src = PresentationSource.FromVisual(this);
                if (src?.CompositionTarget is not null)
                {
                    dpiX = src.CompositionTarget.TransformToDevice.M11;
                    dpiY = src.CompositionTarget.TransformToDevice.M22;
                }
            }
            catch { }

            sb.AppendLine($"[{DateTime.Now}] LAYOUT {stage}: " +
                          $"dpi={dpiX:0.##} " +
                          $"bounds={Left:0},{Top:0} {Width:0}x{Height:0} " +
                          $"window={ActualWidth:0}x{ActualHeight:0} " +
                          $"sidebar={SidebarBorder.ActualWidth:0} " +
                          $"main={MainContentGrid.ActualWidth:0}x{MainContentGrid.ActualHeight:0} " +
                          $"host={(MainFrame?.ActualWidth ?? 0):0}x{(MainFrame?.ActualHeight ?? 0):0} " +
                          $"content={(content?.ActualWidth ?? -1):0}x{(content?.ActualHeight ?? -1):0}");

            if (content is not null)
            {
                sb.AppendLine($"    content type: {content.GetType().Name}, HA={content.HorizontalAlignment}");
                int n = VisualTreeHelper.GetChildrenCount(content);
                for (int i = 0; i < n; i++)
                {
                    if (VisualTreeHelper.GetChild(content, i) is FrameworkElement fe)
                        sb.AppendLine($"    child[{i}] {fe.GetType().Name} w={fe.ActualWidth:0} h={fe.ActualHeight:0} HA={fe.HorizontalAlignment}");
                }
            }

            var path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                ?? AppDomain.CurrentDomain.BaseDirectory, "startup.log");
            System.IO.File.AppendAllText(path, sb.ToString());
        }
        catch { }
    }

    private void OnNavigated(string key) => UpdatePageHeader(key);

    private void UpdatePageHeader(string key)
    {
        if (PageMeta.TryGetValue(key, out var meta))
        {
            PageTitleText.Text = meta.Title;
            PageSubtitleText.Text = meta.Subtitle;
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _vm.CurrentView = tag;
            _nav.NavigateTo(tag);
            UpdatePageHeader(tag);
        }
    }

    // ══════════════ Search filter for sidebar ══════════════
    private void NavSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SidebarStackPanel == null) return;
        ApplyFilter(NavSearchBox.Text.Trim());
    }

    private void ApplyFilter(string query)
    {
        foreach (var element in EnumerateNavItems(SidebarStackPanel))
        {
            if (element is RadioButton rb)
            {
                var match = string.IsNullOrEmpty(query)
                            || GetNavText(rb).Contains(query, StringComparison.OrdinalIgnoreCase);
                rb.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private static IEnumerable<DependencyObject> EnumerateNavItems(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in EnumerateNavItems(child)) yield return d;
        }
    }

    private static string GetNavText(RadioButton rb)
    {
        var parts = new List<string>();
        void Walk(DependencyObject o)
        {
            if (o is TextBlock t && !string.IsNullOrEmpty(t.Text)) parts.Add(t.Text);
            int c = VisualTreeHelper.GetChildrenCount(o);
            for (int i = 0; i < c; i++) Walk(VisualTreeHelper.GetChild(o, i));
        }
        try { Walk(rb); } catch { }
        return string.Join(" ", parts);
    }

    // ══════════════ Top bar shortcuts ══════════════
    private void GoToUpdate_Click(object sender, RoutedEventArgs e) => NavigateTo("update");
    private void GoToSettings_Click(object sender, RoutedEventArgs e) => NavigateTo("settings");

    /// <summary>Public navigation method for child views (e.g. DashboardView quick actions).</summary>
    public void NavigateTo(string key)
    {
        _vm.CurrentView = key;
        _nav.NavigateTo(key);
        UpdatePageHeader(key);
        SelectNavItem(key);
    }

    /// <summary>The currently displayed view (Frame content). Used by the render/verification mode.</summary>
    public FrameworkElement? CurrentViewElement => MainFrame?.Content as FrameworkElement;

    private void SelectNavItem(string key)
    {
        foreach (var element in EnumerateNavItems(SidebarStackPanel))
        {
            if (element is RadioButton rb && rb.Tag as string == key)
            {
                rb.IsChecked = true;
                break;
            }
        }
    }
}
