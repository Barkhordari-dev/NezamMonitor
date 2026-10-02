using System.Windows;

namespace NezamMonitor.App.ViewModels;

public static class NavigationCommandHelper
{
    public static bool? ConfirmAction(string message, string title)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes ? true : (result == MessageBoxResult.No ? false : (bool?)null);
    }
}