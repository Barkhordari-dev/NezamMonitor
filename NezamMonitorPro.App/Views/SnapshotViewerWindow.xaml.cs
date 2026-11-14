using System.Windows;

namespace NezamMonitorPro.App.Views;

/// <summary>Read-only snapshot viewer window (P3). Shows one snapshot's data; closes without changing anything.</summary>
public partial class SnapshotViewerWindow : Window
{
    public SnapshotViewerWindow(ViewModels.SnapshotViewerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
