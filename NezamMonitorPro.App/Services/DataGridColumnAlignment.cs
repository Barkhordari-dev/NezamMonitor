using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// راست‌کلیک روی هدر هر جدول → منوی «تراز ستون» (راست‌چین / وسط‌چین /
/// چپ‌چین) برای همان ستون، ذخیره‌شده در Settings، به‌علاوهٔ گزینهٔ
/// «هدر همیشه وسط» که هدر را وسط نگه می‌دارد هرچند متن ستون راست‌چین یا
/// چپ‌چین باشد. ستونی که انتخابی ندارد، تراز کلی تنظیمات
/// (theme_TableAlignment) را دنبال می‌کند؛ «بازگشت به پیش‌فرض» هر دو
/// انتخاب را پاک می‌کند. تراز کلی همچنان فقط از منوی تنظیمات تغییر می‌کند.
///
/// روی همهٔ DataGridها فعال است: از طریق استایل ProDataGrid (App.xaml آن را
/// استایل پیش‌فرض همهٔ جدول‌ها کرده).
///
/// نحوهٔ اعمال: برای ستون متنی یک ElementStyle جدید بر پایهٔ استایل اصلی
/// (GridCellEllipsis) با TextAlignment مشخص ساخته می‌شود، به‌علاوهٔ CellStyle
/// و HeaderStyle بر پایهٔ استایل‌های اصلی جدول با HorizontalContentAlignment
/// متناظر — پس هم متن سلول، هم جای متن در سلول، و هم متن هدر جابه‌جا می‌شود.
/// استایل‌های اصلی هر ستون در اولین Loaded ذخیره می‌شوند تا «پیش‌فرض» دقیقاً
/// همان حالت XAML را برگرداند.
///
/// نکتهٔ مهم — TextAlignment در WPF نسبت به جهت جریان متن است، نه فیزیکی:
/// در پاراگراف راست‌به‌چپ، Left روی لبهٔ فیزیکی راست و Right روی لبهٔ
/// فیزیکی چپ می‌نشیند؛ درحالی‌که HorizontalContentAlignment فیزیکی است.
/// اگر هر دو را یک مقدار می‌گذاشتیم، متن سلول و هدر در جهت مخالف هم قرار
/// می‌گرفتند (همین باگی که گزارش شد). پس مقدار TextAlignment در RTL
/// برعکس می‌شود تا هر دو روی یک جهت فیزیکی بنشینند.
/// </summary>
public static class DataGridColumnAlignment
{
    public static readonly DependencyProperty EnableHeaderMenuProperty =
        DependencyProperty.RegisterAttached("EnableHeaderMenu", typeof(bool),
            typeof(DataGridColumnAlignment), new PropertyMetadata(false, OnEnableHeaderMenuChanged));

    public static bool GetEnableHeaderMenu(DependencyObject obj) => (bool)obj.GetValue(EnableHeaderMenuProperty);
    public static void SetEnableHeaderMenu(DependencyObject obj, bool value) => obj.SetValue(EnableHeaderMenuProperty, value);

    /// <summary>Original styles of one grid's columns + identity for the settings key.</summary>
    private sealed class GridState
    {
        public Dictionary<DataGridColumn, (Style? Element, Style? Cell, Style? Header)> Originals { get; } = new();
        public string ViewName { get; set; } = "view";
        public string GridName { get; set; } = "grid";
        public bool Initialized { get; set; }
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached("State", typeof(GridState),
            typeof(DataGridColumnAlignment), new PropertyMetadata(null));

    private static GridState GetState(DataGrid grid)
    {
        if (grid.GetValue(StateProperty) is not GridState state)
        {
            state = new GridState();
            grid.SetValue(StateProperty, state);
        }
        return state;
    }

    private static void OnEnableHeaderMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        if ((bool)e.NewValue)
        {
            grid.Loaded += Grid_Loaded;
            grid.PreviewMouseRightButtonUp += Grid_PreviewMouseRightButtonUp;
        }
        else
        {
            grid.Loaded -= Grid_Loaded;
            grid.PreviewMouseRightButtonUp -= Grid_PreviewMouseRightButtonUp;
        }
    }

    private static void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        var grid = (DataGrid)sender;
        var state = GetState(grid);
        if (state.Initialized) return; // cached view re-attached: originals + styles already in place
        state.Initialized = true;
        state.GridName = string.IsNullOrEmpty(grid.Name) ? "grid" : grid.Name;
        DependencyObject? p = grid;
        while (p != null && p is not UserControl) p = VisualTreeHelper.GetParent(p);
        state.ViewName = p is UserControl uc ? uc.GetType().Name : "view";

        foreach (var col in grid.Columns)
            state.Originals[col] = (
                col is DataGridBoundColumn bc ? bc.ElementStyle : null,
                col.CellStyle,
                col.HeaderStyle);

        for (int i = 0; i < grid.Columns.Count; i++)
        {
            var saved = LoadAlignment(state, i, grid.Columns[i]);
            if (saved is not null)
                ApplyAlignment(grid, state, i, grid.Columns[i], saved.Value, save: false);
            else if (LoadHeaderCentered(state, i, grid.Columns[i]))
                ApplyHeaderCentered(grid, state, grid.Columns[i]);
        }
    }

    // ── Right-click on a header ──────────────────────────────────────
    private static void Grid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;
        var header = FindHeader(e.OriginalSource as DependencyObject, grid);
        if (header?.Column is null) return; // row/empty click: the view's own menu handles it
        var col = header.Column;
        var state = GetState(grid);
        var index = grid.Columns.IndexOf(col);
        var current = LoadAlignment(state, index, col);
        var headerCentered = LoadHeaderCentered(state, index, col);

        var font = Application.Current?.Resources["FontFamily"] as FontFamily
                   ?? new FontFamily("B Nazanin");
        var menu = new ContextMenu { FlowDirection = FlowDirection.RightToLeft, FontFamily = font, FontSize = 14 };

        AddItem(menu, "راست‌چین", current == TextAlignment.Right,
            () => ApplyAlignment(grid, state, index, col, TextAlignment.Right, save: true));
        AddItem(menu, "وسط‌چین", current == TextAlignment.Center,
            () => ApplyAlignment(grid, state, index, col, TextAlignment.Center, save: true));
        AddItem(menu, "چپ‌چین", current == TextAlignment.Left,
            () => ApplyAlignment(grid, state, index, col, TextAlignment.Left, save: true));
        menu.Items.Add(new Separator());
        AddItem(menu, "هدر همیشه وسط", headerCentered, () =>
        {
            // Toggle the header-center pin, then re-apply whatever column
            // alignment is active (or the default if there is none).
            SaveHeaderCentered(state, index, col, !headerCentered);
            var cur = LoadAlignment(state, index, col);
            if (cur is not null) ApplyAlignment(grid, state, index, col, cur.Value, save: false);
            else ClearAlignment(grid, state, index, col);
        });
        menu.Items.Add(new Separator());
        AddItem(menu, "بازگشت به پیش‌فرض (تراز کلی تنظیمات)", current is null && !headerCentered,
            () => ClearAll(grid, state, index, col));

        menu.PlacementTarget = header;
        menu.IsOpen = true;
        // Suppress the row context menu (e.g. Generator's MouseRightButtonUp
        // handler): a header click must open only this menu.
        e.Handled = true;
    }

    private static void AddItem(ContextMenu menu, string header, bool isChecked, Action onClick)
    {
        var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked };
        item.Click += (_, _) => onClick();
        menu.Items.Add(item);
    }

    private static DataGridColumnHeader? FindHeader(DependencyObject? hit, DataGrid grid)
    {
        while (hit != null && hit != grid)
        {
            if (hit is DataGridColumnHeader header) return header;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    // ── Applying ─────────────────────────────────────────────────────
    /// <summary>Maps a PHYSICAL direction to the TextAlignment value that
    /// produces it in the given flow direction (see the class comment).</summary>
    private static TextAlignment ToTextAlignment(TextAlignment physical, FlowDirection flow)
        => flow != FlowDirection.RightToLeft ? physical : physical switch
        {
            TextAlignment.Right => TextAlignment.Left,
            TextAlignment.Left => TextAlignment.Right,
            _ => TextAlignment.Center,
        };

    private static void ApplyAlignment(DataGrid grid, GridState state, int index,
        DataGridColumn col, TextAlignment physicalAlign, bool save)
    {
        var originals = state.Originals.TryGetValue(col, out var o)
            ? o : (Element: (Style?)null, Cell: (Style?)null, Header: (Style?)null);
        var hAlign = physicalAlign switch
        {
            TextAlignment.Right => HorizontalAlignment.Right,
            TextAlignment.Left => HorizontalAlignment.Left,
            _ => HorizontalAlignment.Center,
        };

        // Text columns: text alignment inside the cell's TextBlock.
        // (Flow-aware: see ToTextAlignment — without the inversion the text
        // landed on the opposite side from the header.)
        if (col is DataGridBoundColumn bc && originals.Element is not null)
        {
            var es = new Style { TargetType = typeof(TextBlock), BasedOn = originals.Element };
            es.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,
                ToTextAlignment(physicalAlign, grid.FlowDirection)));
            bc.ElementStyle = es;
        }
        // Cell: where the content block itself sits in the cell (physical).
        var cs = new Style { TargetType = typeof(DataGridCell), BasedOn = originals.Cell ?? grid.CellStyle };
        cs.Setters.Add(new Setter(DataGridCell.HorizontalContentAlignmentProperty, hAlign));
        col.CellStyle = cs;

        // Header: follows the column, unless it is pinned to the center.
        var headerAlign = LoadHeaderCentered(state, index, col) ? HorizontalAlignment.Center : hAlign;
        var hs = new Style { TargetType = typeof(DataGridColumnHeader), BasedOn = originals.Header ?? grid.ColumnHeaderStyle };
        hs.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, headerAlign));
        col.HeaderStyle = hs;

        if (save) SaveAlignment(state, index, col, physicalAlign);
    }

    /// <summary>Header-only centered override (used when the column has no
    /// alignment of its own but its header is pinned to the center).</summary>
    private static void ApplyHeaderCentered(DataGrid grid, GridState state, DataGridColumn col)
    {
        var originals = state.Originals.TryGetValue(col, out var o)
            ? o : (Element: (Style?)null, Cell: (Style?)null, Header: (Style?)null);
        var hs = new Style { TargetType = typeof(DataGridColumnHeader), BasedOn = originals.Header ?? grid.ColumnHeaderStyle };
        hs.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        col.HeaderStyle = hs;
    }

    /// <summary>Restores the original styles (the header-center pin, if set,
    /// is re-applied on top).</summary>
    private static void ClearAlignment(DataGrid grid, GridState state, int index, DataGridColumn col)
    {
        var originals = state.Originals.TryGetValue(col, out var o)
            ? o : (Element: (Style?)null, Cell: (Style?)null, Header: (Style?)null);
        if (col is DataGridBoundColumn bc) bc.ElementStyle = originals.Element;
        col.CellStyle = originals.Cell;
        col.HeaderStyle = originals.Header;
        if (LoadHeaderCentered(state, index, col)) ApplyHeaderCentered(grid, state, col);
    }

    /// <summary>Clears both the column alignment and the header-center pin.</summary>
    private static void ClearAll(DataGrid grid, GridState state, int index, DataGridColumn col)
    {
        SaveAlignment(state, index, col, null);
        SaveHeaderCentered(state, index, col, false);
        ClearAlignment(grid, state, index, col);
    }

    // ── Persistence ──────────────────────────────────────────────────
    // Keyed by view + grid + declaration index + header text: the index
    // keeps two same-named columns apart, and the key follows the column
    // (not its position) when the user reorders columns. A checkbox column's
    // header is a CheckBox, not a string, so it gets a fixed key part.
    private static string HeaderKeyPart(DataGridColumn col)
        => col.Header is string s && s.Length > 0 ? s
         : col is DataGridCheckBoxColumn ? "checkbox" : col.GetType().Name;

    private static string Key(GridState state, int index, DataGridColumn col)
        => $"col_align_{state.ViewName}_{state.GridName}_{index}_{HeaderKeyPart(col)}";

    private static string HeaderCenterKey(GridState state, int index, DataGridColumn col)
        => $"col_align_hdr_center_{state.ViewName}_{state.GridName}_{index}_{HeaderKeyPart(col)}";

    private static TextAlignment? LoadAlignment(GridState state, int index, DataGridColumn col)
    {
        var v = DatabaseService.Instance.GetSetting(Key(state, index, col));
        return v switch
        {
            "Right" => TextAlignment.Right,
            "Center" => TextAlignment.Center,
            "Left" => TextAlignment.Left,
            _ => null,
        };
    }

    private static void SaveAlignment(GridState state, int index, DataGridColumn col, TextAlignment? align)
    {
        try
        {
            DatabaseService.Instance.SaveSetting(Key(state, index, col), align switch
            {
                TextAlignment.Right => "Right",
                TextAlignment.Center => "Center",
                TextAlignment.Left => "Left",
                _ => "",
            });
        }
        catch { }
    }

    private static bool LoadHeaderCentered(GridState state, int index, DataGridColumn col)
        => DatabaseService.Instance.GetSetting(HeaderCenterKey(state, index, col)) == "1";

    private static void SaveHeaderCentered(GridState state, int index, DataGridColumn col, bool centered)
    {
        try { DatabaseService.Instance.SaveSetting(HeaderCenterKey(state, index, col), centered ? "1" : ""); }
        catch { }
    }
}
