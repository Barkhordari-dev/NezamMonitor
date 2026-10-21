using System.Windows.Controls;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

public partial class ChangesView : UserControl
{
    public ChangesView()
    {
        InitializeComponent();
        DataContext = new ChangesViewModel(DatabaseService.Instance);
    }
}
