using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// هدر ستون چک‌باکس انتخاب ردیف‌ها → یک چک‌باکس واقعی: با کلیک روی آن همه
/// ردیف‌های جدول (فیلتر فعلی) تیک می‌خورند، با کلیک دوباره تیکشان برداشته
/// می‌شود. حالت آن سه‌حالته است (همه / هیچ‌کدام / بعضی) و با تغییر تیک هر
/// ردیف — از دکمه‌های «انتخاب همه»، تک‌کلیک روی ردیف یا چک‌باکس خود ردیف —
/// همگام می‌شود. روی همه جدول‌ها فعال است (استایل ProDataGrid)؛ فقط روی
/// اولین DataGridCheckBoxColumn که به IsSelected بایند شده اعمال می‌شود.
///
/// اگر هدر اصلی ستون یک کلمه بود (مثل «انتخاب» در حق‌الزحمه)، چک‌باکس کنار
/// همان کلمه می‌نشیند؛ اگر فقط علامت ☐ بود، فقط چک‌باکس می‌آید.
/// </summary>
public static class SelectAllHeaderBehavior
{
    public static readonly DependencyProperty EnableSelectAllProperty =
        DependencyProperty.RegisterAttached("EnableSelectAll", typeof(bool),
            typeof(SelectAllHeaderBehavior), new PropertyMetadata(false, OnEnableSelectAllChanged));

    public static bool GetEnableSelectAll(DependencyObject obj) => (bool)obj.GetValue(EnableSelectAllProperty);
    public static void SetEnableSelectAll(DependencyObject obj, bool value) => obj.SetValue(EnableSelectAllProperty, value);

    private sealed class GridState
    {
        public DataGridCheckBoxColumn? Column;
        public CheckBox? HeaderBox;
        public string? OriginalLabel;
        public PropertyInfo? IsSelectedProp;
        public Dictionary<object, PropertyChangedEventHandler> ItemHandlers { get; } = new();
        public INotifyCollectionChanged? Collection;
        public NotifyCollectionChangedEventHandler? CollectionHandler;
        public bool Applying;
        public bool Initialized;
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached("State", typeof(GridState),
            typeof(SelectAllHeaderBehavior), new PropertyMetadata(null));

    private static GridState GetState(DataGrid grid)
    {
        if (grid.GetValue(StateProperty) is not GridState state)
        {
            state = new GridState();
            grid.SetValue(StateProperty, state);
        }
        return state;
    }

    private static void OnEnableSelectAllChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        if ((bool)e.NewValue)
        {
            grid.Loaded += Grid_Loaded;
            grid.Unloaded += Grid_Unloaded;
        }
        else
        {
            grid.Loaded -= Grid_Loaded;
            grid.Unloaded -= Grid_Unloaded;
        }
    }

    private static void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        var grid = (DataGrid)sender;
        var state = GetState(grid);
        if (!state.Initialized)
        {
            state.Initialized = true;
            SetupHeader(grid, state);
        }
        if (state.Column is not null)
        {
            Subscribe(grid, state);
            UpdateHeaderState(grid, state);
        }
    }

    private static void Grid_Unloaded(object sender, RoutedEventArgs e)
        => Unsubscribe(GetState((DataGrid)sender));

    private static void SetupHeader(DataGrid grid, GridState state)
    {
        // The row-selection checkbox column (the only checkbox column bound
        // to IsSelected). Other checkbox columns, if any, are left alone.
        var col = grid.Columns.OfType<DataGridCheckBoxColumn>()
            .FirstOrDefault(c => (c.Binding as System.Windows.Data.Binding)?.Path.Path == "IsSelected");
        if (col is null) return;

        var itemType = GetItemType(grid.ItemsSource)
                       ?? grid.ItemsSource?.Cast<object>().FirstOrDefault()?.GetType();
        var prop = itemType?.GetProperty("IsSelected",
            BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || prop.PropertyType != typeof(bool) || !prop.CanWrite) return;
        state.Column = col;
        state.IsSelectedProp = prop;
        state.OriginalLabel = col.Header as string;

        var font = Application.Current?.Resources["FontFamily"] as FontFamily
                   ?? new FontFamily("B Nazanin");
        var box = new CheckBox
        {
            IsThreeState = true,
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
        };
        box.Click += (_, _) => ToggleAll(grid, state);
        state.HeaderBox = box;

        // Keep the original label word (e.g. «انتخاب») next to the box;
        // a bare ☐ symbol is replaced by the box alone.
        if (!string.IsNullOrEmpty(state.OriginalLabel) && state.OriginalLabel != "☐")
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            panel.Children.Add(box);
            panel.Children.Add(new TextBlock
            {
                Text = state.OriginalLabel,
                FontFamily = font,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            col.Header = panel;
        }
        else
        {
            col.Header = box;
        }

        // Centre the header checkbox: the column is narrow and the default
        // right alignment would stick it to the edge. (This runs before the
        // column-alignment behavior's Loaded, so the centred style is what
        // that behavior records as the column's original header style.)
        try
        {
            if (grid.FindResource("GridHeaderCenter") is Style center)
                col.HeaderStyle = center;
        }
        catch { }
    }

    // ── Toggle ───────────────────────────────────────────────────────
    private static void ToggleAll(DataGrid grid, GridState state)
    {
        if (state.IsSelectedProp is null) return;
        var items = grid.ItemsSource?.Cast<object>().ToList() ?? new List<object>();
        if (items.Count == 0) return;
        // Decide from the rows, not from the box's own toggled state:
        // if everything is already selected, deselect all; otherwise select all.
        var allSelected = items.All(i => (bool)state.IsSelectedProp.GetValue(i)!);
        state.Applying = true;
        try
        {
            foreach (var item in items)
                state.IsSelectedProp.SetValue(item, !allSelected);
        }
        finally { state.Applying = false; }
        UpdateHeaderState(grid, state);
    }

    private static void UpdateHeaderState(DataGrid grid, GridState state)
    {
        if (state.HeaderBox is null || state.IsSelectedProp is null) return;
        var items = grid.ItemsSource?.Cast<object>().ToList() ?? new List<object>();
        if (items.Count == 0) { state.HeaderBox.IsChecked = false; return; }
        var selected = items.Count(i => (bool)state.IsSelectedProp.GetValue(i)!);
        state.HeaderBox.IsChecked = selected == 0 ? false
            : selected == items.Count ? true : (bool?)null;
    }

    // ── Sync (rows → header box) ─────────────────────────────────────
    private static void Subscribe(DataGrid grid, GridState state)
    {
        Unsubscribe(state);
        if (grid.ItemsSource is INotifyCollectionChanged incc)
        {
            state.Collection = incc;
            state.CollectionHandler = (_, _) =>
            {
                SubscribeItems(grid, state);
                UpdateHeaderState(grid, state);
            };
            incc.CollectionChanged += state.CollectionHandler;
        }
        SubscribeItems(grid, state);
    }

    private static void SubscribeItems(DataGrid grid, GridState state)
    {
        if (state.IsSelectedProp is null) return;
        foreach (var item in grid.ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>())
        {
            if (item is INotifyPropertyChanged npc && !state.ItemHandlers.ContainsKey(item))
            {
                PropertyChangedEventHandler h = (_, e) =>
                {
                    if (state.Applying) return;
                    if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "IsSelected")
                        UpdateHeaderState(grid, state);
                };
                state.ItemHandlers[item] = h;
                npc.PropertyChanged += h;
            }
        }
    }

    private static void Unsubscribe(GridState state)
    {
        foreach (var (item, h) in state.ItemHandlers)
            if (item is INotifyPropertyChanged npc) npc.PropertyChanged -= h;
        state.ItemHandlers.Clear();
        if (state.Collection is not null && state.CollectionHandler is not null)
            state.Collection.CollectionChanged -= state.CollectionHandler;
        state.Collection = null;
        state.CollectionHandler = null;
    }

    private static Type? GetItemType(IEnumerable? source)
    {
        if (source is null) return null;
        var iface = source.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return iface?.GetGenericArguments()[0];
    }
}
