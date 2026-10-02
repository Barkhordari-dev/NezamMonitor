using System.Windows.Controls;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class ChangesView : UserControl
{
    public ChangesView()
    {
        InitializeComponent();
        DataContext = new ChangesViewModel(DatabaseService.Instance);
    }
}
