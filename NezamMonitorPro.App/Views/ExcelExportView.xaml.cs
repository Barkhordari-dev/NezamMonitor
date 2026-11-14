using System.Windows.Controls;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace NezamMonitorPro.App.Views;

public partial class ExcelExportView : UserControl
{
    public ExcelExportView()
    {
        InitializeComponent();
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        DataContext = App.Services.GetRequiredService<ExcelExportViewModel>();
    }
}
