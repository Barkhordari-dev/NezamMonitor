using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace NezamMonitorPro.App.Views;

public partial class GeneratorView : UserControl, INavigationAware
{
    // The view is rebuilt on every navigation (NavigationService only
    // caches update/history/cases); keep the ViewModel alive so the heavy
    // template/scan loads run once, like the cached pages.
    private static GeneratorViewModel? _cachedVm;

    public GeneratorView()
    {
        InitializeComponent();
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        _cachedVm ??= App.Services.GetRequiredService<GeneratorViewModel>();
        DataContext = _cachedVm;
        // Keep the header checkbox in sync when the history rows reload
        // (LoadHistory clears and re-adds them, losing all checkmarks).
        ((INotifyCollectionChanged)HistoryGrid.Items).CollectionChanged += (_, _) => { SyncHistoryHeaderCheckBox(); RefreshHistorySelectionState(); };
        HistoryGrid.SelectionChanged += (_, _) => RefreshHistorySelectionState();
        ((INotifyCollectionChanged)CaseGrid.Items).CollectionChanged += (_, _) => { SyncCaseSelectAllCheckBox(); RefreshCaseCountDisplay(); };
        SyncCaseSelectAllCheckBox();
    }

    /// <summary>Called on every navigation to this page (cached ViewModel, fresh view).</summary>
    public void OnNavigatedTo()
    {
        if (DataContext is not GeneratorViewModel vm) return;
        vm.RefreshIfSnapshotChanged();
        if (vm.ActiveTab == 1)
            vm.LoadHistory();
    }

    /// <summary>شمارنده «X انتخاب‌شده از Y» را بعد از تغییر ردیف‌ها به‌روز می‌کند.</summary>
    private void RefreshCaseCountDisplay()
    {
        if (DataContext is GeneratorViewModel vm) vm.RefreshCountDisplay();
    }

    /// <summary>وضعیت انتخاب تب تاریخچه را از جدول می‌خواند و به ViewModel می‌دهد.</summary>
    private void RefreshHistorySelectionState()
    {
        if (DataContext is not GeneratorViewModel vm) return;
        var checkedCount = HistoryGrid.Items.Cast<ReportHistoryItem>().Count(i => i.IsSelected);
        vm.UpdateHistorySelection(checkedCount, HistoryGrid.SelectedItems.Count);
    }

    /// <summary>
    /// Scales the two grids' height caps with the UI zoom: at 100% the caps
    /// are 350/400px as before, but zoomed out the caps grow (350/scale), so
    /// the smaller rows leave room for MORE rows in the same visual height
    /// instead of just shrinking the same ~9 rows. Called by MainWindow
    /// whenever the zoom changes and when navigating to this view.
    /// </summary>
    public void ApplyZoom(double scale)
    {
        if (scale <= 0) scale = 1.0;
        CaseGrid.MaxHeight = 350 / scale;
        HistoryGrid.MaxHeight = 400 / scale;
    }

    private void StageFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int stage))
        {
            if (DataContext is GeneratorViewModel vm)
                vm.SelectedStage = stage;
        }
    }

    private void FilterStage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && DataContext is GeneratorViewModel vm)
            vm.FilterStage = cb.SelectedIndex;
    }

    private void FilterStatus_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && DataContext is GeneratorViewModel vm)
            vm.FilterStatus = cb.SelectedIndex;
    }

    private void CaseGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        // Act on the row under the mouse, not on whatever row was selected before.
        var hit = dg.InputHitTest(e.GetPosition(dg));
        CaseReportItem? item = null;
        if (hit is DependencyObject depObj)
        {
            var current = depObj;
            while (current != null)
            {
                if (current is DataGridRow row && row.Item is CaseReportItem rowItem)
                {
                    item = rowItem;
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        if (item == null) return;
        dg.SelectedItem = item;
        {
            var menu = new ContextMenu();

            // تولید گزارش این پرونده
            var genItem = new MenuItem { Header = "📝 تولید گزارش", Tag = item };
            genItem.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                {
                    if (GeneratorViewModel.IsProducedStatus(item.Status))
                    {
                        // Already produced: batch Generate would skip it; rebuild it explicitly.
                        vm.RegenerateCaseReport(item);
                        return;
                    }
                    item.IsSelected = true;
                    vm.GenerateCommand.Execute(null);
                    item.IsSelected = false;
                }
            };
            menu.Items.Add(genItem);

            // باز کردن پوشه پرونده
            var openFolder = new MenuItem { Header = "📂 باز کردن پوشه پرونده", Tag = item };
            openFolder.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                    vm.OpenFolderForCase(item);
            };
            menu.Items.Add(openFolder);

            // مشاهده گزارش‌های این پرونده
            var viewReports = new MenuItem { Header = "📚 مشاهده گزارش‌های این پرونده", Tag = item };
            viewReports.Click += (s, args) =>
            {
                if (DataContext is GeneratorViewModel vm)
                {
                    vm.HistorySearchText = item.CaseNumber;
                    vm.ActiveTab = 1;
                    vm.LoadHistory();
                }
            };
            menu.Items.Add(viewReports);

            menu.Items.Add(new Separator());

            // کپی شماره پرونده
            var copyCase = new MenuItem { Header = "📋 کپی شماره پرونده", Tag = item };
            copyCase.Click += (s, args) =>
            {
                Clipboard.SetText(item.CaseNumber);
                if (DataContext is GeneratorViewModel vm)
                    vm.StatusMessage = $"شماره پرونده کپی شد: {item.CaseNumber}";
            };
            menu.Items.Add(copyCase);

            // کپی نام مالک
            var copyOwner = new MenuItem { Header = "📋 کپی نام مالک", Tag = item };
            copyOwner.Click += (s, args) =>
            {
                Clipboard.SetText(item.Owner);
                if (DataContext is GeneratorViewModel vm)
                    vm.StatusMessage = $"نام مالک کپی شد: {item.Owner}";
            };
            menu.Items.Add(copyOwner);

            menu.IsOpen = true;
        }
    }

    private void HistoryGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid dg)
        {
            // انتخاب ردیف راست‌کلیک‌شده اگه قبلا انتخاب نشده
            var hit = dg.InputHitTest(e.GetPosition(dg));
            if (hit is DependencyObject depObj)
            {
                // پیدا کردن DataGridRow از طریق visual tree
                var current = depObj;
                while (current != null)
                {
                    if (current is DataGridRow row)
                    {
                        if (row.Item is ReportHistoryItem item && !dg.SelectedItems.Contains(item))
                            dg.SelectedItem = item;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }
    }

    // ─── Helper: دریافت آیتم‌های انتخاب‌شده ───
    // Checked rows win (the batch operations act on them). When nothing is
    // checked, fall back to the clicked/selected row, so right-click on a
    // single row still works without checking it first.
    private List<ReportHistoryItem> GetSelectedHistoryItems()
    {
        var checkedItems = HistoryGrid.Items.Cast<ReportHistoryItem>().Where(i => i.IsSelected).ToList();
        if (checkedItems.Count > 0) return checkedItems;
        var selected = HistoryGrid.SelectedItems;
        if (selected == null || selected.Count == 0)
        {
            if (HistoryGrid.SelectedItem is ReportHistoryItem single)
                return new List<ReportHistoryItem> { single };
            return new List<ReportHistoryItem>();
        }
        return selected.Cast<ReportHistoryItem>().ToList();
    }

    // ─── History header checkbox: check/uncheck all visible rows ───
    // The flag stops the programmatic sync (below) from re-triggering these
    // handlers and unchecking everything again.
    private bool _syncingHistoryHeaderCheckBox;

    private void HistoryHeaderCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingHistoryHeaderCheckBox) return;
        foreach (var item in HistoryGrid.Items.Cast<ReportHistoryItem>())
            item.IsSelected = true;
        if (DataContext is GeneratorViewModel vm)
            vm.StatusMessage = $"همه {HistoryGrid.Items.Count} ردیف انتخاب شد";
        RefreshHistorySelectionState();
    }

    private void HistoryHeaderCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_syncingHistoryHeaderCheckBox) return;
        foreach (var item in HistoryGrid.Items.Cast<ReportHistoryItem>())
            item.IsSelected = false;
        if (DataContext is GeneratorViewModel vm)
            vm.StatusMessage = "انتخاب لغو شد";
        RefreshHistorySelectionState();
    }

    private void HistoryRowCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        SyncHistoryHeaderCheckBox();
        RefreshHistorySelectionState();
    }

    // ─── Generate grid header checkbox: check/uncheck all visible rows ───
    private bool _syncingCaseSelectAllCheckBox;

    private void CaseSelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingCaseSelectAllCheckBox) return;
        SetAllCaseRowsSelected(true);
    }

    private void CaseSelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_syncingCaseSelectAllCheckBox) return;
        SetAllCaseRowsSelected(false);
    }

    private void SetAllCaseRowsSelected(bool selected)
    {
        foreach (var item in CaseGrid.Items.Cast<CaseReportItem>())
            item.IsSelected = selected;
        RefreshCaseCountDisplay();
    }

    private void CaseRowCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        SyncCaseSelectAllCheckBox();
        RefreshCaseCountDisplay();
    }

    private void SyncCaseSelectAllCheckBox()
    {
        if (CaseSelectAllCheckBox == null) return;
        var items = CaseGrid.Items.Cast<CaseReportItem>().ToList();
        _syncingCaseSelectAllCheckBox = true;
        try
        {
            CaseSelectAllCheckBox.IsChecked = items.Count > 0 && items.All(i => i.IsSelected);
        }
        finally
        {
            _syncingCaseSelectAllCheckBox = false;
        }
    }

    // ─── Production card header: the whole header toggles the card ───
    private void ProductionHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The toggle button handles its own click; ignore clicks that start
        // on it so one click doesn't toggle twice.
        if (IsInside(e.OriginalSource as DependencyObject, ProductionToggleButton)) return;
        if (DataContext is GeneratorViewModel vm)
            vm.ToggleProductionCommand.Execute(null);
    }

    private static bool IsInside(DependencyObject? source, DependencyObject target)
    {
        var current = source;
        while (current != null)
        {
            if (current == target) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void SyncHistoryHeaderCheckBox()
    {
        if (HistorySelectAllCheckBox == null) return;
        var items = HistoryGrid.Items.Cast<ReportHistoryItem>().ToList();
        var all = items.Count > 0 && items.All(i => i.IsSelected);
        _syncingHistoryHeaderCheckBox = true;
        HistorySelectAllCheckBox.IsChecked = all;
        _syncingHistoryHeaderCheckBox = false;
    }

    // ─── Event Handlers for XAML ContextMenu ───

    private void HistoryMenu_OpenReport(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.OpenSelectedHistory(items);
    }

    private void HistoryMenu_OpenFolder(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.OpenSelectedHistoryFolder(items);
    }

    private void HistoryMenu_Regenerate(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.RegenerateSelectedHistory(items);
    }

    private void HistoryMenu_ConvertToPdf(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.ConvertSelectedToPdf(items);
    }

    private void HistoryMenu_DeleteSelected(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistory(items);
    }

    private void HistoryMenu_DeleteWithFile(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistoryWithFile(items);
    }

    // ─── Event Handlers for XAML Toolbar Buttons ───

    private void History_OpenSelected(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.OpenSelectedHistory(items);
    }

    private void History_ConvertSelectedToPdf(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.ConvertSelectedToPdf(items);
    }

    private void History_OpenSelectedFolder(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.OpenSelectedHistoryFolder(items);
    }

    private void History_RegenerateSelected(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.RegenerateSelectedHistory(items);
    }

    private void History_DeleteSelected(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistory(items);
    }

    private void History_DeleteSelectedWithFile(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0)
        {
            if (DataContext is GeneratorViewModel vm0) vm0.StatusMessage = "موردی انتخاب نشده — ابتدا ردیف‌ها را انتخاب کنید";
            return;
        }
        if (DataContext is GeneratorViewModel vm) vm.DeleteSelectedHistoryWithFile(items);
    }

    private void HistoryMenu_CopyPath(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedHistoryItems();
        if (items.Count == 0) return;
        Clipboard.SetText(items[0].OutputPath);
        if (DataContext is GeneratorViewModel vm)
            vm.StatusMessage = $"مسیر کپی شد: {items[0].OutputPath}";
    }
}
