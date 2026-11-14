using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// کلاس تنظیمات هر مرحله گزارش
/// </summary>
public class StageConfiguration : INotifyPropertyChanged
{
    private int _configuredMonths;
    public int StageNumber { get; set; }
    public string StageTitle { get; set; } = "";
    public int DefaultMonths { get; set; }
    public string DeadlineType { get; set; } = "";
    public string BuildingGroup { get; set; } = "";
    public string Discipline { get; set; } = "";
    public Action<int>? OnConfiguredMonthsChanged { get; set; }

    public int ConfiguredMonths
                {
                    get => _configuredMonths;
                    set
                    {
                        if (_configuredMonths != value && value >= 1 && value <= 30)
                        {
                            _configuredMonths = value;
                            OnPropertyChanged();
                            OnConfiguredMonthsChanged?.Invoke(value);
                        }
                    }
                }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
