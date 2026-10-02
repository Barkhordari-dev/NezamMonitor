using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class EngineersView : UserControl
{
    public EngineersView()
    {
        InitializeComponent();
        DataContext = new EngineersViewModel(Services.DatabaseService.Instance);
    }

    private void EngineersGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(dg, hit);
        if (cell == null) return;

        var item = cell.DataContext as EngineerItem;
        if (item == null) return;

        var menu = new ContextMenu();
        var colIndex = dg.Columns.IndexOf(cell.Column);
        var colHeader = cell.Column.Header.ToString() ?? "";

        // Determine column name and value from the model
        string? columnName = null;
        string? cellValue = colHeader switch
        {
            "پرونده" => item.CaseNumber,
            "مالک" => item.Owner,
            "رشته" => item.Discipline,
            "ناظر" => item.Name,
            _ => null
        };
        if (cellValue != null) columnName = colHeader switch
        {
            "پرونده" => "CaseNumber", "مالک" => "Owner", "رشته" => "Discipline", "ناظر" => "Engineer", _ => ""
        };

        if (!string.IsNullOrWhiteSpace(cellValue) && !string.IsNullOrEmpty(columnName))
        {
            var filterItem = new MenuItem { Header = $"🔍 فیلتر بر اساس «{cellValue}»" };
            filterItem.Click += (_, _) => (DataContext as EngineersViewModel)?.FilterByValueCommand.Execute($"{columnName}:{cellValue}");
            menu.Items.Add(filterItem);

            var removeItem = new MenuItem { Header = $"❌ لغو فیلتر {colHeader}" };
            removeItem.Click += (_, _) => (DataContext as EngineersViewModel)?.RemoveFilter(columnName);
            menu.Items.Add(removeItem);

            menu.Items.Add(new Separator());
            var copyItem = new MenuItem { Header = $"📋 کپی «{cellValue}»" };
            copyItem.Click += (_, _) => Clipboard.SetText(cellValue);
            menu.Items.Add(copyItem);
        }

        if (menu.HasItems) menu.IsOpen = true;
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
