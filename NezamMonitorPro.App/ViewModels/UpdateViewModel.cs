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

    // آخرین مقادیری که واقعا در دیتابیس ذخیره شده‌اند؛
    // DUP-005: با هر کلید فشرده‌شده دیگر در دیتابیس ذخیره نمی‌کنیم.
    private string _savedUsername = "";
    private string _savedPassword = "";

    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public bool RememberMe { get => _rememberMe; set {
        if (_rememberMe == value) return;
        SetProperty(ref _rememberMe, value);
        try
        {
            _db.SaveSetting(SettingKeys.RememberMe, value ? "true" : "false");
            if (!value)
            {
                // SEC-006: خاموش‌کردن «مرا به خاطر بسپار» رمز ذخیره‌شده را پاک می‌کند.
                DeleteSetting(_db, SettingKeys.Password);
                _savedPassword = "";
            }
        }
        catch { }
    } }

    /// <summary>
    /// نام کاربری و رمز را فقط وقتی واقعا عوض شده‌اند در دیتابیس ذخیره می‌کند.
    /// (موقع شروع به‌روزرسانی و موقع بستن صفحه صدا زده می‌شود.)
    /// </summary>
    public void SaveCredentials()
    {
        try
        {
            if (_rememberMe)
            {
                if (_username != _savedUsername)
                {
                    _db.SaveSetting(SettingKeys.Username, _username);
                    _savedUsername = _username;
                }
                if (!string.IsNullOrEmpty(_password) && _password != _savedPassword)
                {
                    _db.SaveSetting(SettingKeys.Password, _password);
                    _savedPassword = _password;
                }
            }
            else if (_savedPassword.Length > 0)
            {
                // وقتی «مرا به خاطر بسپار» خاموش است، رمز ذخیره‌شده پاک می‌شود.
                DeleteSetting(_db, SettingKeys.Password);
                _savedPassword = "";
            }
        }
        catch { }
    }

    /// <summary>یک تنظیم را از جدول Settings حذف می‌کند (NezamDatabase متد حذف ندارد).</summary>
    internal static void DeleteSetting(NezamDatabase db, string key)
    {
        // ARCH-003: دسترسی مستقیم به اتصال مشترک زیر قفل اتصال انجام می‌شود.
        lock (db.SyncRoot)
        {
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Settings WHERE Key = @k";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.ExecuteNonQuery();
        }
    }

    public UpdateViewModel(NezamDatabase db)
    {
        _db = db;
        try
        {
            var settings = _db.LoadSettings();
            if (settings.TryGetValue(SettingKeys.Username, out var u))
            {
                _username = u;
                _savedUsername = u;
            }
            if (settings.TryGetValue(SettingKeys.RememberMe, out var r)) _rememberMe = r == "true";
            // SEC-006: رمز فقط وقتی خوانده می‌شود که «مرا به خاطر بسپار» روشن باشد.
            if (_rememberMe && settings.TryGetValue(SettingKeys.Password, out var p))
            {
                _password = p;
                _savedPassword = p;
            }
            else if (settings.ContainsKey(SettingKeys.Password))
            {
                // رمز به‌جامانده از نسخه‌های قبل، وقتی «مرا به خاطر بسپار» خاموش است، پاک می‌شود.
                DeleteSetting(_db, SettingKeys.Password);
            }
        }
        catch { }
    }
}
