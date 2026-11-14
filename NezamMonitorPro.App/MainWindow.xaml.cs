using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App;

public partial class MainWindow : Window
{
    private string GetLayoutPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "column_layout.json");
    private readonly MainViewModel _vm;
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

    private double _uiScale = 1.0;
    private readonly ScaleTransform _uiScaleTransform = new ScaleTransform(1.0, 1.0);

    private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            if (e.Delta > 0) _uiScale += 0.1;
            else _uiScale -= 0.1;

            // Zoom range 50%–200%: the old 70%–150% clamp (original code)
            // stopped the user from zooming out enough on small monitors
            // and from zooming in enough to read small text.
            _uiScale = Math.Max(0.5, Math.Min(2.0, _uiScale));
            ApplyZoom();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Bubble-phase wheel forwarding: the plain wheel only reaches a
    /// scrollable control when the mouse is directly over it, so over the
    /// filter bar, headers and other dead areas nothing scrolled at all.
    /// This handler runs AFTER inner controls: if one already handled the
    /// wheel (e.Handled), it never fires, so nothing ever scrolls twice.
    /// Otherwise it scrolls the current page's primary table (first
    /// DataGrid, else first ScrollViewer) — 3 lines per notch vertically,
    /// or ~120px horizontally with Shift. Ctrl+wheel is zoom and is
    /// handled in the preview phase instead.
    /// </summary>
    private void Window_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) return;
        try
        {
            var target = FindPrimaryScrollable(MainFrame?.Content as DependencyObject);
            if (target == null) return;
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
            {
                double offset = target.HorizontalOffset + (e.Delta > 0 ? -120 : 120);
                offset = Math.Max(0, Math.Min(target.ScrollableWidth, offset));
                target.ScrollToHorizontalOffset(offset);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    if (e.Delta > 0) target.LineUp();
                    else target.LineDown();
                }
            }
            e.Handled = true;
        }
        catch { }
    }

    /// <summary>First DataGrid's inner ScrollViewer wins; otherwise the
    /// first ScrollViewer in the page. Null on pages with nothing
    /// scrollable (e.g. the short Excel form).</summary>
    private static ScrollViewer? FindPrimaryScrollable(DependencyObject? root)
    {
        if (root == null) return null;
        var grid = FindVisualChild<DataGrid>(root);
        if (grid != null)
        {
            var inner = FindVisualChild<ScrollViewer>(grid);
            if (inner != null) return inner;
        }
        return FindVisualChild<ScrollViewer>(root);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ctrl+0 resets the zoom to 100%.
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control
            && e.Key == System.Windows.Input.Key.D0)
        {
            _uiScale = 1.0;
            ApplyZoom();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Applies the current zoom scale (50%–200%). The scale itself is a
    /// LayoutTransform (vector, so text is re-rendered, not stretched as a
    /// bitmap). Rendering NEVER changes with zoom: Display + ClearType +
    /// layout rounding stay on at every level. The old code switched to
    /// Ideal/Grayscale and turned layout rounding off while zoomed, which
    /// is exactly what made zoomed Persian text look blurry with colour
    /// fringing. At 100% the outer ScrollViewer stays disabled so the
    /// pages' own DataGrid scrollbars keep working and the Cases
    /// GridSplitter keeps its size; while zoomed it scrolls both ways so
    /// the enlarged content stays reachable.
    /// </summary>
    private void ApplyZoom()
    {
        _uiScaleTransform.ScaleX = _uiScale;
        _uiScaleTransform.ScaleY = _uiScale;
        bool zoomed = Math.Abs(_uiScale - 1.0) > 0.001;
        // L-003: زوم فقط روی محتوای صفحه اعمال می‌شود (نه نوار بالا و وضعیت)؛
        // وقتی زوم است، محتوای بزرگ‌شده با اسکرول دیده می‌شود (به‌جای بریده شدن).
        // در زوم ۱۰۰٪ اسکرول غیرفعال است تا چیدمان ستاره‌ای صفحه‌ها عوض نشود.
        // Rendering is fixed at every zoom level (see doc comment above).
        TextOptions.SetTextFormattingMode(ContentBorder, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(ContentBorder, TextRenderingMode.ClearType);
        ContentBorder.UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(ContentBorder, BitmapScalingMode.Linear);
        ContentScrollViewer.VerticalScrollBarVisibility = zoomed ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        ContentScrollViewer.HorizontalScrollBarVisibility = zoomed ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        SyncGeneratorZoom();
    }

    /// <summary>L-003: برگرداندن بزرگ‌نمایی به ۱۰۰٪ (دکمه نوار بالا + Ctrl+0).</summary>
    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _uiScale = 1.0;
        ApplyZoom();
        SaveZoom();
    }

    /// <summary>Persists the current zoom level (ui_zoom).</summary>
    private void SaveZoom()
    {
        try
        {
            DatabaseService.Instance.SaveSetting(SettingKeys.UiZoom,
                _uiScale.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch { }
    }

    /// <summary>Keeps GeneratorView's grid height caps in step with the zoom
    /// (see GeneratorView.ApplyZoom): the caps must grow when zooming out,
    /// otherwise the tables just shrink instead of showing more rows.</summary>
    private void SyncGeneratorZoom()
    {
        try
        {
            if (MainFrame?.Content is Views.GeneratorView gv)
                gv.ApplyZoom(_uiScale);
        }
        catch { }
    }

    public MainWindow()
    {
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        _vm = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        // Fix: the fixed 1440x900 design size is taller than smaller screens
        // (e.g. 1366x768, or 125% scaling), so CenterScreen pushes the title
        // bar above the visible area and the minimize/maximize/close buttons
        // disappear. Clamp the window (and its minimums) to the work area so
        // the title bar is always reachable.
        var area = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, area.Width);
        MinHeight = Math.Min(MinHeight, area.Height);
        Width = Math.Min(Width, area.Width);
        Height = Math.Min(Height, area.Height);
        // On narrow screens (e.g. 1024x768) the 264px sidebar eats a quarter
        // of the width and filter bars run out of room. Slim it down so the
        // content area keeps ~800px; the user can still drag it wider.
        if (area.Width < 1200)
        {
            SidebarColumn.MinWidth = 200;
            SidebarColumn.Width = new GridLength(210);
        }
        ApplyResponsiveTopBar(area.Width);
        this.PreviewMouseWheel += Window_PreviewMouseWheel;
        this.MouseWheel += Window_MouseWheel;
        this.PreviewKeyDown += Window_PreviewKeyDown;
        this.SizeChanged += MainWindow_SizeChanged;
        // Zoom is persisted (ui_zoom) so a restart keeps the level the user
        // picked; a missing or bad value falls back to 100%.
        try
        {
            var savedZoom = DatabaseService.Instance.GetSetting(SettingKeys.UiZoom);
            if (double.TryParse(savedZoom, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double z))
                _uiScale = Math.Max(0.5, Math.Min(2.0, z));
        }
        catch { }
        this.Closing += (_, _) => SaveZoom();
        // L-003: بزرگ‌نمایی روی محتوای صفحه است، نه کل پنجره.
        ContentBorder.LayoutTransform = _uiScaleTransform;
        ApplyZoom();
        DataContext = _vm;
        _nav = App.Navigation;
        _nav.SetFrame(MainFrame);

        _themeManager = new ThemeManager(DatabaseService.Instance);
        _themeManager.LoadTheme();
        _themeManager.ApplyTheme(_themeManager.CurrentTheme);
        Services.ButtonStyleManager.ApplySaved(DatabaseService.Instance);
        ApplyUiCustomization();

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

        EnsureContentColumnStar();
        Loaded += (_, _) =>
        {
            EnsureContentColumnStar();
            UpdateDbStatusDot();
            ShowFirstRunGuideIfNeeded();
        };
    }

    /// <summary>
    /// L-001: با هر تغییر اندازه، حداقل‌ها دوباره با ناحیه کاری تطبیق داده
    /// می‌شوند (مثلا وقتی مانیتور عوض شد)؛ در عرض باریک سایدبار باریک می‌ماند
    /// و هینت کلیدها کوتاه می‌شود (L-019).
    /// </summary>
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        try
        {
            var area = SystemParameters.WorkArea;
            if (MinWidth > area.Width) MinWidth = area.Width;
            if (MinHeight > area.Height) MinHeight = area.Height;
            ApplyResponsiveTopBar(ActualWidth);
            if (ActualWidth < 1200)
            {
                if (SidebarColumn.Width.Value > 210)
                {
                    SidebarColumn.MinWidth = 200;
                    SidebarColumn.Width = new GridLength(210);
                }
                if (KeyHintText != null) KeyHintText.Text = "F5 به‌روزرسانی";
            }
            else if (KeyHintText != null)
            {
                KeyHintText.Text = "F5 به‌روزرسانی · Ctrl+D داشبورد · Ctrl+O تنظیمات سریع";
            }
        }
        catch { }
    }

    /// <summary>
    /// Narrow top bar: below 1200px the global search (200px) plus the
    /// connection pill plus three 38px buttons overflowed the content area
    /// at the 960px minimum width and clipped the page title. Shrink the
    /// search to 140px and hide the pill (the green dot in the footer
    /// still shows the DB status); the three icon buttons always stay.
    /// </summary>
    private void ApplyResponsiveTopBar(double width)
    {
        try
        {
            bool narrow = width < 1200;
            if (GlobalSearchBox != null) GlobalSearchBox.Width = narrow ? 140 : 200;
            if (ConnectionStatusPill != null)
                ConnectionStatusPill.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        }
        catch { }
    }

    /// <summary>
    /// A11Y-005: نقطه سبز فوتر تزیینی نیست؛ سلامت واقعی دیتابیس را نشان
    /// می‌دهد (PRAGMA integrity_check). اگر مشکلی باشد قرمز می‌شود.
    /// </summary>
    private void UpdateDbStatusDot()
    {
        try
        {
            var result = DatabaseService.Instance.CheckIntegrity();
            var ok = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            var app = Application.Current;
            if (DbStatusDot != null && app != null)
                DbStatusDot.Fill = ok
                    ? (Brush)app.Resources["SuccessBrush"]
                    : (Brush)app.Resources["ErrorBrush"];
            if (DbStatusText != null)
            {
                DbStatusText.Text = ok ? "دیتابیس محلی · رمزنگاری‌شده · سالم" : "دیتابیس محلی · مشکل دارد";
                DbStatusText.ToolTip = ok
                    ? "بررسی سلامت دیتابیس با موفقیت انجام شد."
                    : "دیتابیس مشکل دارد؛ از صفحه تنظیمات پشتیبان بگیر.";
            }
        }
        catch { }
    }

    /// <summary>
    /// M-6: راهنمای شروع اول — فقط بار اول بعد از لود پنجره اصلی،
    /// اگر تنظیم first_run_done ست نشده باشد.
    /// </summary>
    private void ShowFirstRunGuideIfNeeded()
    {
        try
        {
            if (DatabaseService.Instance.GetSetting(SettingKeys.FirstRunDone) == "1") return;
            var guide = new Views.FirstRunGuideWindow { Owner = this };
            guide.ShowDialog();
            if (guide.DoNotShowAgain)
                DatabaseService.Instance.SaveSetting(SettingKeys.FirstRunDone, "1");
        }
        catch { }
    }

    /// <summary>
    /// B-011: F5 — تازه‌سازی صفحه جاری. صفحه‌هایی که تازه‌سازی خودشان را
    /// دارند (به‌روزرسانی، تولید گزارش، تنظیمات و…) از دکمه‌های خودشان
    /// تازه می‌شوند؛ F5 آن‌ها را دست نمی‌زند.
    /// </summary>
    public void RefreshCurrentPage()
    {
        try
        {
            var dc = (MainFrame?.Content as FrameworkElement)?.DataContext;
            switch (dc)
            {
                case ViewModels.DashboardViewModel d:
                    d.RefreshCommand.Execute(null); _vm.StatusMessage = "داشبورد به‌روز شد"; break;
                case ViewModels.CasesViewModel c:
                    c.RefreshCommand.Execute(null); _vm.StatusMessage = "پرونده‌ها به‌روز شد"; break;
                case ViewModels.EngineersViewModel e:
                    e.RefreshCommand.Execute(null); _vm.StatusMessage = "مهندسین به‌روز شد"; break;
                case ViewModels.FeesViewModel f:
                    f.RefreshCommand.Execute(null); _vm.StatusMessage = "حق‌الزحمه به‌روز شد"; break;
                case ViewModels.ReportsViewModel r:
                    r.RefreshCommand.Execute(null); _vm.StatusMessage = "گزارش‌ها به‌روز شد"; break;
                case ViewModels.FollowUpViewModel fu:
                    fu.RefreshCommand.Execute(null); _vm.StatusMessage = "پیگیری به‌روز شد"; break;
                case ViewModels.ChangesViewModel ch:
                    ch.CompareCommand.Execute(null); _vm.StatusMessage = "مقایسه دوباره انجام شد"; break;
                default:
                    _vm.StatusMessage = "برای به‌روزرسانی این صفحه از دکمه‌های خود صفحه استفاده کن.";
                    break;
            }
        }
        catch { }
    }

    // ══════════════ M-9: جستجوی سراسری ══════════════
    private string? _pendingCasesSearch;

    private void GlobalSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        _pendingCasesSearch = GlobalSearchBox.Text.Trim();
        NavigateTo("cases");
        ApplyPendingCasesSearch();
        e.Handled = true;
    }

    /// <summary>عبارت جستجو را در صفحه پرونده‌ها می‌گذارد (همان جدول فیلترشده).</summary>
    private void ApplyPendingCasesSearch()
    {
        if (_pendingCasesSearch is null) return;
        if (MainFrame?.Content is Views.CasesView cv && cv.DataContext is ViewModels.CasesViewModel vm)
        {
            vm.SearchText = _pendingCasesSearch;
            _pendingCasesSearch = null;
        }
    }

    /// <summary>
    /// ستون ۱ (محتوای اصلی) باید همیشه * باشد. بعضی مسیرها (مثلا اعمال تم از صفحه تنظیمات)
    /// عرض ستون‌ها را بازنویسی می‌کردند و محتوا به ۲۶۴ پیکسل مچاله می‌شد.
    /// </summary>
    private void EnsureContentColumnStar()
    {
        try
        {
            if (Content is not Grid grid || grid.ColumnDefinitions.Count < 3) return;

            var sidebar = grid.ColumnDefinitions[0];
            // L-002: آستانه برگرداندن ۱۸۰ → ۱۵۰ و هدف ۲۶۴ → ۲۱۰؛ سایدبار
            // ۱۵۰ تا ۲۱۰ باریک عمدی کاربر است و نباید به ۲۶۴ برگردد.
            if (!sidebar.Width.IsStar && sidebar.Width.Value < 150)
                sidebar.Width = new GridLength(210);

            var content = grid.ColumnDefinitions[2];
            if (!content.Width.IsStar)
                content.Width = new GridLength(1, GridUnitType.Star);
        }
        catch { }
    }


    private void OnNavigated(string key)
    {
        // M-9: اگر جستجوی سراسری در انتظار است (مثلا اولین ورود به صفحه
        // پرونده‌ها که اول صفحه بارگذاری نشان داده می‌شود)، حالا اعمالش کن.
        ApplyPendingCasesSearch();
        UpdatePageHeader(key);
    }

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

    private readonly HashSet<string> _hiddenNavTags = new(StringComparer.Ordinal);

    /// <summary>
    /// Applies the hidden menu's UI customisation: renamed / hidden sidebar
    /// items (by Tag) and the «نسخه پرو» badges. Called at startup and
    /// after a change in the hidden settings.
    /// </summary>
    public void ApplyUiCustomization()
    {
        try
        {
            var custom = Services.UiCustomizationService.Load(DatabaseService.Instance);
            _hiddenNavTags.Clear();
            foreach (var element in EnumerateNavItems(SidebarStackPanel))
            {
                if (element is not System.Windows.Controls.RadioButton nav) continue;
                var tag = nav.Tag?.ToString() ?? "";
                if (!custom.NavItems.TryGetValue(tag, out var item)) continue;
                if (!item.Visible) _hiddenNavTags.Add(tag);
                // The label is the second TextBlock in the content StackPanel.
                if (nav.Content is System.Windows.Controls.StackPanel sp
                    && sp.Children.Count > 1
                    && sp.Children[1] is System.Windows.Controls.TextBlock label)
                {
                    label.Text = string.IsNullOrWhiteSpace(item.Title)
                        ? Services.UiCustomizationService.NavDefaults.FirstOrDefault(d => d.Tag == tag).Title ?? label.Text
                        : item.Title;
                }
            }
            ApplyFilter(NavSearchBox?.Text?.Trim() ?? "");
            var hide = custom.HideProBadge;
            if (SidebarProBadge != null) SidebarProBadge.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (TopProBadge != null) TopProBadge.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        catch { }
    }

    private void ApplyFilter(string query)
    {
        foreach (var element in EnumerateNavItems(SidebarStackPanel))
        {
            if (element is RadioButton rb)
            {
                var tag = rb.Tag?.ToString() ?? "";
                var match = (string.IsNullOrEmpty(query)
                            || GetNavText(rb).Contains(query, StringComparison.OrdinalIgnoreCase))
                            && !_hiddenNavTags.Contains(tag);
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
        SyncGeneratorZoom();
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
