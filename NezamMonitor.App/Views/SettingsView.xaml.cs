using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(new Services.ThemeManager(Services.DatabaseService.Instance), Services.DatabaseService.DatabasePath);
    }

    // Color Presets
    private void PresetBlue_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.PrimaryHex = "#FF3498DB";
            vm.SecondaryHex = "#FF2ECC71";
            vm.AccentHex = "#FF9B59B6";
            vm.BackgroundHex = "#FFF5F5F5";
            vm.SurfaceHex = "#FFFFFFFF";
            vm.TextHex = "#FF2C3E50";
            vm.SidebarHex = "#FF2C3E50";
        }
    }

    private void PresetGreen_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.PrimaryHex = "#FF27AE60";
            vm.SecondaryHex = "#FF2ECC71";
            vm.AccentHex = "#FF16A085";
            vm.BackgroundHex = "#FFF0FFF0";
            vm.SurfaceHex = "#FFFFFFFF";
            vm.TextHex = "#FF1E3A2F";
            vm.SidebarHex = "#FF1E3A2F";
        }
    }

    private void PresetPurple_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.PrimaryHex = "#FF9B59B6";
            vm.SecondaryHex = "#FF8E44AD";
            vm.AccentHex = "#FFE74C3C";
            vm.BackgroundHex = "#FFF8F0FF";
            vm.SurfaceHex = "#FFFFFFFF";
            vm.TextHex = "#FF2C3E50";
            vm.SidebarHex = "#FF2C3E50";
        }
    }

    private void PresetWarm_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.PrimaryHex = "#FFE74C3C";
            vm.SecondaryHex = "#FFF39C12";
            vm.AccentHex = "#FFE67E22";
            vm.BackgroundHex = "#FFFFF5F5";
            vm.SurfaceHex = "#FFFFFFFF";
            vm.TextHex = "#FF2C3E50";
            vm.SidebarHex = "#FF2C3E50";
        }
    }

    private void PresetDark_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.PrimaryHex = "#FF3498DB";
            vm.SecondaryHex = "#FF2ECC71";
            vm.AccentHex = "#FF9B59B6";
            vm.BackgroundHex = "#FF1E1E1E";
            vm.SurfaceHex = "#FF2D2D2D";
            vm.TextHex = "#FFECF0F1";
            vm.SidebarHex = "#FF191919";
            vm.SidebarTextHex = "#FFDCDCDC";
        }
    }

    // Direction Presets
    private void PresetRTL_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.TextDirection = "RightToLeft";
            vm.TableDirection = "RightToLeft";
            vm.MenuDirection = "RightToLeft";
            vm.MainWindowDirection = "RightToLeft";
            vm.ApplyDirectionCommand.Execute(null);
        }
    }

    private void PresetLTR_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.TextDirection = "LeftToRight";
            vm.TableDirection = "LeftToRight";
            vm.MenuDirection = "LeftToRight";
            vm.MainWindowDirection = "LeftToRight";
            vm.ApplyDirectionCommand.Execute(null);
        }
    }

    private void PresetMixed_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.TextDirection = "RightToLeft";
            vm.TableDirection = "LeftToRight";
            vm.MenuDirection = "RightToLeft";
            vm.MainWindowDirection = "RightToLeft";
            vm.ApplyDirectionCommand.Execute(null);
        }
    }
}
