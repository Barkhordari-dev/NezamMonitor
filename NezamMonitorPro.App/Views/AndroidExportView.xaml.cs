using System.Windows.Controls;

namespace NezamMonitorPro.App.Views;

public partial class AndroidExportView : UserControl
{
    public AndroidExportView()
    {
        InitializeComponent();
        DataContext = new ViewModels.AndroidExportViewModel();
    }
}
