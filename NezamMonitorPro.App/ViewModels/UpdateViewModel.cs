using System.ComponentModel;
using System.Windows.Input;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.ViewModels;

public sealed class UpdateViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _username = "";
    private string _password = "";
    private bool _rememberMe;

    public string Username { get => _username; set {
        if (_username == value) return;
        SetProperty(ref _username, value);
        try { _db.SaveSetting("username", value); } catch { }
    } }
    public string Password { get => _password; set {
        if (_password == value) return;
        SetProperty(ref _password, value);
        // The saved password only stays saved while "remember me" is on.
        try { if (_rememberMe && value.Length > 0) _db.SaveSetting("password", value); } catch { }
    } }
    public bool RememberMe { get => _rememberMe; set {
        if (_rememberMe == value) return;
        SetProperty(ref _rememberMe, value);
        try { _db.SaveSetting("remember_me", value ? "true" : "false"); } catch { }
    } }

    public UpdateViewModel(NezamDatabase db)
    {
        _db = db;
        try
        {
            var settings = _db.LoadSettings();
            if (settings.TryGetValue("username", out var u)) _username = u;
            if (settings.TryGetValue("password", out var p)) _password = p;
            if (settings.TryGetValue("remember_me", out var r)) _rememberMe = r == "true";
        }
        catch { }
    }
}
