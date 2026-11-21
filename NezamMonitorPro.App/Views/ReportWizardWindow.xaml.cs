using System.Diagnostics;
using System.IO;
using System.Windows;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.App.Views;

/// <summary>Modal window for the step-by-step report wizard. Opened from
/// the Generator view's Generate grid — right-click a row, «صدور مرحله‌ای گزارش».</summary>
public partial class ReportWizardWindow : Window
{
    private readonly ReportWizardViewModel _vm;

    public ReportWizardWindow(Case c, int rowNumber)
    {
        InitializeComponent();
        _vm = new ReportWizardViewModel(DatabaseService.Instance, c, rowNumber);
        DataContext = _vm;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _vm.GoBack();

    private void NextButton_Click(object sender, RoutedEventArgs e) => _vm.GoNext();

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        => await _vm.GenerateAsync();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void AddDescButton_Click(object sender, RoutedEventArgs e) => _vm.AddDescriptionRow();

    private void RemoveDescButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b && b.Tag is WizardDescRow row)
            _vm.RemoveDescriptionRow(row);
    }

    private void ImportFollowUpButton_Click(object sender, RoutedEventArgs e) => _vm.ImportFromFollowUp();

    private void ShowFollowUpButton_Click(object sender, RoutedEventArgs e) => _vm.ShowFollowUp();

    private void AddToFollowUpButton_Click(object sender, RoutedEventArgs e) => _vm.AddSelectedToFollowUp();

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_vm.GeneratedPath) && File.Exists(_vm.GeneratedPath))
                Process.Start(new ProcessStartInfo(_vm.GeneratedPath) { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(_vm.GeneratedPath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch { }
    }
}
