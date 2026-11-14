using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NezamMonitorPro.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace NezamMonitorPro.App.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        DataContext = App.Services.GetRequiredService<ReportsViewModel>();
    }

    private void ReportsGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(dg, hit);
        if (cell == null) return;

        var item = cell.DataContext as ReportItem;
        if (item == null) return;

        var vm = DataContext as ReportsViewModel;
        var menu = new ContextMenu();

        // ─── Report actions (replaces the old "مشاهده" button column) ───
        var openItem = new MenuItem { Header = "📄 باز کردن گزارش" };
        openItem.Click += (_, _) => vm?.OpenReportCommand.Execute(item);
        menu.Items.Add(openItem);

        var folderItem = new MenuItem { Header = "📂 باز کردن پوشه گزارش" };
        folderItem.Click += (_, _) => vm?.OpenFolderCommand.Execute(item);
        menu.Items.Add(folderItem);

        // Download only makes sense for a server report with an image.
        if (item.Source == "server" && !string.IsNullOrEmpty(item.ImageName))
        {
            var downloadItem = new MenuItem { Header = "⬇ دانلود و باز کردن گزارش" };
            downloadItem.Click += (_, _) => vm?.DownloadAndOpenCommand.Execute(item);
            menu.Items.Add(downloadItem);
        }

        // G-006: متن هدر از TextBlock خوانده می‌شود، نه Header.ToString()
        // (برای ستون‌هایی که هدرشان TextBlock است، ToString مقدار
        // «System.Windows.Controls.TextBlock» می‌داد و فیلتر/کپی کار نمی‌کرد).
        var colHeader = GetHeaderText(cell.Column.Header);

        string? cellValue = colHeader switch
        {
            "پرونده" => item.CaseNumber, "مالک" => item.Owner, "نوع" => item.ReportType,
            "مرحله" => item.Stage, "ناظر" => item.Engineer, "رشته" => item.Discipline,
            "منبع" => item.SourceDisplay, "تاریخ" => item.VisitDate, _ => null
        };
        string? columnName = cellValue != null ? colHeader switch
        {
            "پرونده" => "CaseNumber", "مالک" => "Owner", "نوع" => "ReportType",
            "مرحله" => "Stage", "ناظر" => "Engineer", "رشته" => "Discipline",
            "منبع" => "Source", "تاریخ" => "VisitDate", _ => ""
        } : null;

        if (!string.IsNullOrWhiteSpace(cellValue) && !string.IsNullOrEmpty(columnName))
        {
            menu.Items.Add(new Separator());

            var filterItem = new MenuItem { Header = $"🔍 فیلتر بر اساس «{cellValue}»" };
            filterItem.Click += (_, _) => vm?.FilterByValueCommand.Execute($"{columnName}:{cellValue}");
            menu.Items.Add(filterItem);

            var removeItem = new MenuItem { Header = $"❌ لغو فیلتر {colHeader}" };
            removeItem.Click += (_, _) => vm?.RemoveFilter(columnName);
            menu.Items.Add(removeItem);

            menu.Items.Add(new Separator());
            var copyItem = new MenuItem { Header = $"📋 کپی «{cellValue}»" };
            copyItem.Click += (_, _) => Clipboard.SetText(cellValue);
            menu.Items.Add(copyItem);
        }

        if (menu.HasItems)
        {
            e.Handled = true;
            menu.IsOpen = true;
        }
    }

    private void ReportsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        var hit = e.OriginalSource as DependencyObject;
        var cell = FindDataGridCell(dg, hit);
        if (cell?.DataContext is not ReportItem item) return;
        (DataContext as ReportsViewModel)?.OpenReportCommand.Execute(item);
    }

    /// <summary>متن هدر ستون؛ هدرهای TextBlock (منبع، نوع، مرحله، ناظر، تاریخ) مستقیما Text دارند.</summary>
    private static string GetHeaderText(object? header) => header switch
    {
        TextBlock tb => tb.Text ?? "",
        string s => s,
        _ => header?.ToString() ?? ""
    };

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