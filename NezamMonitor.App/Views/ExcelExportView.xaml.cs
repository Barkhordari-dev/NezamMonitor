using System.Windows.Controls;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class ExcelExportView : UserControl
{
    public ExcelExportView()
    {
        InitializeComponent();
        DataContext = new ExcelExportViewModel(DatabaseService.Instance);
    }
}
