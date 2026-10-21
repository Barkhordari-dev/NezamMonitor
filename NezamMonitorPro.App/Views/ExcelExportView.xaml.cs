using System.Windows.Controls;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

public partial class ExcelExportView : UserControl
{
    public ExcelExportView()
    {
        InitializeComponent();
        DataContext = new ExcelExportViewModel(DatabaseService.Instance);
    }
}
