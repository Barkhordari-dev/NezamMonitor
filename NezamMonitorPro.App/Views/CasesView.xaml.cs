using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ClosedXML.Excel;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

public partial class CasesView : UserControl, INavigationAware
{
    private CasesViewModel _vm;
    private bool _isLoaded = false;
    private bool _suppressColumnSave = false;

    public CasesView()
    {
        InitializeComponent();
        _vm = new CasesViewModel(DatabaseService.Instance);
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            var savedRatio = DatabaseService.Instance.GetSetting("cases_splitter_ratio");
            if (!string.IsNullOrEmpty(savedRatio))
            {
                var parts = savedRatio.Split(':');
                if (parts.Length == 2 && double.TryParse(parts[0], out double t) && double.TryParse(parts[1], out double d) && t > 0 && d > 0)
                {
                    TableRow.Height = new GridLength(t, GridUnitType.Star);
                    DetailRow.Height = new GridLength(d, GridUnitType.Star);
                }
            }
            if (!_isLoaded)
            {
                _isLoaded = true;
                CaptureDefaultColumnLayout();
                RestoreColumnOrder();
                AttachColumnWidthTracking();
                // Scroll to the end only on the very first load. The view is
                // cached now, so Loaded fires on every return — scrolling here
                // would yank the user to the last row each time they came back.
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                {
                    var sv = FindChild<ScrollViewer>(CasesGrid);
                    if (sv != null) sv.ScrollToEnd();
                });
            }
        };
        // NOTE: no ScrollToEnd on Cases refresh on purpose — jumping to the
        // last row on every refresh made the scrollbar position meaningless
        // (the thumb always sat at the bottom after any change).
        CasesGrid.ColumnReordered += (_, _) => { if (_suppressColumnSave) return; SaveColumnOrder(); };
        Unloaded += (_, _) => SaveColumnOrder();
    }

    /// <summary>Called on every navigation (the view is cached):
    /// reloads only when new data was extracted since the last load.</summary>
    public void OnNavigatedTo() => _vm.RefreshIfSnapshotChanged();

    // ── Column layout (order + width) ──────────────────────────────
    // Saved in Settings under "cases_column_order" as
    //   v2;<escaped header>=<displayIndex>:<width>;<escaped header>=...
    // Only columns the user resized by dragging carry a width; the rest
    // have an empty width part and keep their Auto width. Keyed by header
    // text (headers contain no ';' or '='); the old format (comma-joined
    // headers, no widths) is ignored so a stale value can never resurrect
    // the default order, and pre-v2 saves (a width for every column) are
    // treated as order-only so they cannot freeze Auto columns again.
    private Dictionary<string, (int DisplayIndex, DataGridLength Width)>? _defaultColumnLayout;

    // Headers of columns the user actually dragged wider/narrower. Only
    // these get a saved width: every other column stays Auto, so it keeps
    // growing with the window/font instead of freezing at the pixel width
    // it happened to have when the layout was first saved.
    private readonly HashSet<string> _userResizedColumns = new();

    private void CaptureDefaultColumnLayout()
    {
        if (_defaultColumnLayout != null) return;
        _defaultColumnLayout = CasesGrid.Columns
            .Select((c, i) => (Key: ColumnKey(c), Index: i, Width: c.Width))
            .ToDictionary(x => x.Key, x => (x.Index, x.Width));
    }

    private static string ColumnKey(DataGridColumn c)
        => c is DataGridCheckBoxColumn ? "☐" : c.Header?.ToString() ?? "";

    private void AttachColumnWidthTracking()
    {
        // DataGrid has no ColumnWidthChanged event; listen to the Width
        // dependency property of each column so a manual resize is saved too.
        foreach (var col in CasesGrid.Columns)
        {
            var dp = System.ComponentModel.DependencyPropertyDescriptor
                .FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
            dp?.AddValueChanged(col, (_, _) =>
            {
                if (_suppressColumnSave) return;
                // Width changes while restoring/resetting are programmatic
                // (_suppressColumnSave is true then); anything else is the
                // user dragging a column border.
                _userResizedColumns.Add(ColumnKey(col));
                SaveColumnOrder();
            });
        }
    }

    private void SaveColumnOrder()
    {
        try
        {
            // BUG FIX (was: Columns.Select(...)): DataGrid.Columns is always
            // in XAML declaration order — dragging a column only changes its
            // DisplayIndex. Saving the collection order therefore always
            // saved the DEFAULT order, so no reorder ever stuck.
            var parts = CasesGrid.Columns
                .OrderBy(c => c.DisplayIndex)
                .Select(c =>
                {
                    // Width is saved only for columns the user resized by
                    // dragging; the rest keep their Auto width (empty width
                    // part) so they keep growing with the window and font.
                    var w = _userResizedColumns.Contains(ColumnKey(c)) && c.Width.IsAbsolute
                        ? $"{c.Width.Value:0}"
                        : "";
                    return $"{Uri.EscapeDataString(ColumnKey(c))}={c.DisplayIndex}:{w}";
                });
            DatabaseService.Instance.SaveSetting("cases_column_order", "v2;" + string.Join(";", parts));
        }
        catch { }
    }

    private void RestoreColumnOrder()
    {
        var saved = DatabaseService.Instance.GetSetting("cases_column_order");
        if (string.IsNullOrEmpty(saved) || !saved.Contains(';')) return; // old format: ignore
        // v2 = widths are saved only for columns the user resized. Older
        // saves wrote a pixel width for EVERY column, which froze all Auto
        // columns — for those, restore the order only and leave widths Auto.
        var isV2 = saved.StartsWith("v2;");
        var columns = CasesGrid.Columns.ToList();
        _suppressColumnSave = true;
        try
        {
            var seen = new HashSet<string>();
            int displayIndex = 0;
            foreach (var entry in saved.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = entry.IndexOf('=');
                if (eq <= 0) continue;
                var key = Uri.UnescapeDataString(entry[..eq]);
                var rest = entry[(eq + 1)..].Split(':');
                if (rest.Length != 2 || !int.TryParse(rest[0], out _)) continue;
                var col = columns.FirstOrDefault(c => ColumnKey(c) == key);
                if (col == null || !seen.Add(key)) continue;
                col.DisplayIndex = displayIndex++;
                // Restore a saved width only for v2 saves (user-resized
                // columns); the checkbox column keeps its fixed 40.
                // No upper limit on restored widths: a column the user dragged
                // wider than 600px must come back that wide (only the 20px
                // floor stays, so an accidentally collapsed column is not
                // restored at zero width).
                if (isV2 && ColumnKey(col) != "☐" && double.TryParse(rest[1], out double w) && w >= 20)
                {
                    col.Width = new DataGridLength(w);
                    _userResizedColumns.Add(ColumnKey(col));
                }
            }
            // Columns not in the saved layout (e.g. added later) go last,
            // in their XAML order, so DisplayIndex values never collide.
            foreach (var col in columns)
            {
                if (!seen.Contains(ColumnKey(col)))
                    col.DisplayIndex = displayIndex++;
            }
        }
        finally { _suppressColumnSave = false; }
    }

    private void ResetColumns_Click(object sender, RoutedEventArgs e)
    {
        _suppressColumnSave = true;
        try
        {
            // Restore the exact XAML defaults captured on first load:
            // declaration order AND declared width (Auto for most columns).
            if (_defaultColumnLayout != null)
            {
                foreach (var col in CasesGrid.Columns)
                {
                    if (_defaultColumnLayout.TryGetValue(ColumnKey(col), out var d))
                    {
                        col.DisplayIndex = d.DisplayIndex;
                        col.Width = d.Width;
                    }
                }
            }
            else
            {
                for (int i = 0; i < CasesGrid.Columns.Count; i++)
                    CasesGrid.Columns[i].DisplayIndex = i;
            }
        }
        finally { _suppressColumnSave = false; }
        try
        {
            DatabaseService.Instance.SaveSetting("cases_column_order", "");
            _userResizedColumns.Clear();
            _vm.StatusMessage = "چینش ستون‌ها به حالت پیش‌فرض برگشت ✓";
        }
        catch { }
    }

    private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        var tableHeight = TableRow.Height.Value;
        var detailHeight = DetailRow.Height.Value;
        if (tableHeight > 0 && detailHeight > 0)
        {
            var ratio = $"{tableHeight:F1}:{detailHeight:F1}";
            DatabaseService.Instance.SaveSetting("cases_splitter_ratio", ratio);
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void CasesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CasesGrid.SelectedItem is CaseDisplayItem selectedItem)
            _vm.ShowCaseDetails(selectedItem);
    }

    // تک‌کلیک روی ردیف = تیک چک‌باکس
    private void CasesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(CasesGrid, hit);
        if (cell == null) return;
        if (cell.Column is DataGridCheckBoxColumn) return;
        if (cell.DataContext is CaseDisplayItem item)
        {
            item.IsSelected = !item.IsSelected;
        }
    }

    // راست‌کلیک: کپی سلول / کپی ردیف / کپی انتخاب‌شده
    private void CasesGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(CasesGrid, hit);
        if (cell == null) return;
        var colHeader = cell.Column.Header?.ToString() ?? "";
        string cellText = GetCellText(cell);
        var item = cell.DataContext as CaseDisplayItem;

        // Follow the font the user picked in Settings (falls back to B Nazanin).
        var appFont = System.Windows.Application.Current?.Resources["FontFamily"] as FontFamily
                      ?? new FontFamily("B Nazanin");
        var menu = new ContextMenu { FlowDirection = FlowDirection.RightToLeft, FontFamily = appFont, FontSize = 14 };

        if (!string.IsNullOrEmpty(cellText))
        {
            var copyCell = new MenuItem { Header = $"📋 کپی سلول «{Truncate(cellText, 32)}»" };
            copyCell.Click += (_, _) => { try { Clipboard.SetText(cellText); } catch { } };
            menu.Items.Add(copyCell);
        }
        if (item != null)
        {
            var copyRow = new MenuItem { Header = $"📄 کپی ردیف {item.CaseNumber}" };
            copyRow.Click += (_, _) => CopyRowsToClipboard(new[] { item });
            menu.Items.Add(copyRow);
        }
        var sel = GetSelectedOrCurrent(item);
        if (sel.Count > 0)
        {
            var copySel = new MenuItem { Header = $"📋 کپی {sel.Count} ردیف انتخاب‌شده" };
            copySel.Click += (_, _) => CopyRowsToClipboard(sel);
            menu.Items.Add(copySel);

            var exportSel = new MenuItem { Header = $"📤 خروجی اکسل {sel.Count} ردیف" };
            exportSel.Click += (_, _) => ExportRowsToExcel(sel);
            menu.Items.Add(exportSel);
        }
        menu.Items.Add(new Separator());
        var copyAll = new MenuItem { Header = "📋 کپی همه (فیلتر فعلی)" };
        copyAll.Click += (_, _) => CopyRowsToClipboard(_vm.Cases.Where(c => true).ToList());
        menu.Items.Add(copyAll);

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        var sel = _vm.Cases.Where(c => c.IsSelected).ToList();
        if (sel.Count == 0) sel = _vm.Cases.ToList();
        CopyRowsToClipboard(sel);
    }

    private void ExportSelected_Click(object sender, RoutedEventArgs e)
    {
        var sel = _vm.Cases.Where(c => c.IsSelected).ToList();
        if (sel.Count == 0) sel = _vm.Cases.ToList();
        ExportRowsToExcel(sel);
    }

    private List<CaseDisplayItem> GetSelectedOrCurrent(CaseDisplayItem? current)
    {
        var sel = _vm.Cases.Where(c => c.IsSelected).ToList();
        if (sel.Count > 0) return sel;
        if (current != null) return new List<CaseDisplayItem> { current };
        return new List<CaseDisplayItem>();
    }

    private static string GetCellText(DataGridCell cell)
    {
        if (cell.Content is TextBlock tb) return tb.Text ?? "";
        // fallback: walk visual tree
        var txt = FindChild<TextBlock>(cell);
        if (txt != null) return txt.Text ?? "";
        return cell.DataContext?.ToString() ?? "";
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

    private void CopyRowsToClipboard(IReadOnlyList<CaseDisplayItem> rows)
    {
        if (rows.Count == 0) { MessageBox.Show("ردیفی انتخاب نشده.", "کپی", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var headers = CasesGrid.Columns.Where(c => c is not DataGridCheckBoxColumn && c.Visibility == Visibility.Visible)
                        .Select(c => c.Header?.ToString() ?? "").ToList();
        var lines = new List<string> { string.Join("\t", headers) };
        foreach (var r in rows)
        {
            var vals = new List<string>();
            foreach (var col in CasesGrid.Columns)
            {
                if (col is DataGridCheckBoxColumn || col.Visibility != Visibility.Visible) continue;
                vals.Add(GetValueForColumn(r, col));
            }
            lines.Add(string.Join("\t", vals));
        }
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
            _vm.StatusMessage = $"{rows.Count} ردیف کپی شد ✓";
        }
        catch (Exception ex) { MessageBox.Show($"خطا در کپی: {ex.Message}"); }
    }

    private static string GetValueForColumn(CaseDisplayItem r, DataGridColumn col)
    {
        var h = col.Header?.ToString() ?? "";
        return h switch
        {
            "ردیف" => r.RowNumber.ToString(),
            "شماره پرونده" => r.CaseNumber,
            "شماره سریال" => r.Serial,
            "نام مالک" => r.Owner,
            "همراه مالک" => r.OwnerMobile,
            "نام پدر" => r.OwnerFather,
            "کد ملی" => r.OwnerNationalCode,
            "تلفن مالک" => r.OwnerTel,
            "آدرس مالک" => r.OwnerAddress,
            "کد پستی" => r.OwnerZip,
            "زادگاه" => r.OwnerBirthLoc,
            "نوع کاربری" => r.UsageType,
            "گروه ساختمانی" => r.BuildingGroup,
            "کد نوسازی" => r.RenovationCode,
            "شماره دستور نقشه" => r.PlanInstructionNo,
            "نوع دستور نقشه" => r.PlanInstructionType,
            "تاریخ دستور نقشه" => r.PlanInstructionDate,
            "مساحت زمین" => r.LandArea,
            "متراژ پاراف" => r.ParafArea,
            "متراژ کسر ظرفیت" => r.CapacityArea,
            "نوع سازه" => r.StructureType,
            "عنوان بلوک" => r.BlockTitle,
            "تعداد بلوک" => r.BlockCount,
            "طبقات" => r.Floors,
            "واحدها" => r.Units,
            "صادرکننده پروانه" => r.Issuer,
            "شماره پروانه" => r.PermitNumber,
            "تاریخ صدور پروانه" => r.PermitDate,
            "تاریخ ترخیص" => r.ReleaseDate,
            "محدوده طرح" => r.PlanZone,
            "آدرس" => r.Address,
            "دفتر" => r.Office,
            "تاریخ کسر ظرفیت" => r.CapacityDate,
            "مسئولیت" => r.Responsibility,
            _ => ""
        };
    }

    private void ExportRowsToExcel(IReadOnlyList<CaseDisplayItem> rows)
    {
        if (rows.Count == 0) { MessageBox.Show("ردیفی برای خروجی وجود ندارد."); return; }
        var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"پرونده‌ها_{DateTime.Now:yyyy-MM-dd}.xlsx" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("پرونده‌ها");
            ws.RightToLeft = true;
            ws.Style.Font.FontName = "B Nazanin";
            ws.Style.Font.FontSize = 14;

            var cols = CasesGrid.Columns.Where(c => c is not DataGridCheckBoxColumn && c.Visibility == Visibility.Visible).ToList();
            // header
            for (int i = 0; i < cols.Count; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = cols[i].Header?.ToString() ?? "";
                c.Style.Font.Bold = true;
                c.Style.Font.FontColor = XLColor.White;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B3147");
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c.Style.Border.OutsideBorderColor = XLColor.FromHtml("#2C3E50");
            }
            ws.Row(1).Height = 26;

            for (int r = 0; r < rows.Count; r++)
            {
                int row = r + 2;
                bool odd = r % 2 == 0;
                var bg = odd ? XLColor.FromHtml("#F8F9FA") : XLColor.White;
                for (int ci = 0; ci < cols.Count; ci++)
                {
                    var cell = ws.Cell(row, ci + 1);
                    cell.Value = GetValueForColumn(rows[r], cols[ci]);
                    cell.Style.Font.FontName = "B Nazanin";
                    cell.Style.Font.FontSize = 14;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D5D8DC");
                    cell.Style.Fill.BackgroundColor = bg;
                }
                ws.Row(row).Height = 20;
            }
            ws.Columns(1, cols.Count).AdjustToContents();
            for (int ci = 1; ci <= cols.Count; ci++) ws.Column(ci).Width = Math.Max(ws.Column(ci).Width, 12);
            ws.RangeUsed()!.SetAutoFilter();
            ws.SheetView.FreezeRows(1);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            wb.SaveAs(dlg.FileName);
            _vm.StatusMessage = $"خروجی اکسل ذخیره شد: {dlg.FileName}";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); } catch { }
        }
        catch (Exception ex) { MessageBox.Show($"خطا در خروجی اکسل: {ex.Message}"); }
    }

    private static DataGridCell? FindDataGridCell(DataGrid dg, DependencyObject? hit)
    {
        while (hit != null && hit != dg)
        {
            if (hit is DataGridCell cell) return cell;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }
}
