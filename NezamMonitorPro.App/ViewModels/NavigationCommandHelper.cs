using System.Windows;

namespace NezamMonitorPro.App.ViewModels;

public static class NavigationCommandHelper
{
    public static bool? ConfirmAction(string message, string title)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes ? true : (result == MessageBoxResult.No ? false : (bool?)null);
    }
}