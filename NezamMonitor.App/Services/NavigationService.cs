namespace NezamMonitor.App.Services;

/// <summary>Simple frame-based navigation using dictionary of view factories.</summary>
public sealed class NavigationService
{
    private readonly Dictionary<string, Func<System.Windows.Controls.UserControl>> _factories = new();
    private System.Windows.Controls.Frame? _frame;
    private string _currentKey = "";

    public event Action<string>? Navigated;
    public string CurrentKey => _currentKey;

    public void Register(string key, Func<System.Windows.Controls.UserControl> factory)
        => _factories[key] = factory;

    public void SetFrame(System.Windows.Controls.Frame frame)
        => _frame = frame;

    public void NavigateTo(string key)
    {
        if (_frame is null) return;
        if (_factories.TryGetValue(key, out var factory))
        {
            try
            {
                _frame.Content = factory();
                _currentKey = key;
                Navigated?.Invoke(key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error for {key}: {ex.Message}");
                _currentKey = key;
            }
        }
    }

    public bool CanNavigate(string key) => _factories.ContainsKey(key);
}
