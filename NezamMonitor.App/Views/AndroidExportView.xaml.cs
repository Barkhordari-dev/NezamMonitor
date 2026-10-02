using System.Windows.Controls;

namespace NezamMonitor.App.Views;

public partial class AndroidExportView : UserControl
{
    public AndroidExportView()
    {
        InitializeComponent();
        DataContext = new ViewModels.AndroidExportViewModel();
    }
}
