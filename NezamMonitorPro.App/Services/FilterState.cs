using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// وضعیت مشترک فیلترها (پیش‌نویس + اعمال‌شده) برای صفحه‌های Cases و Fees و
/// FollowUp (L-016 / DUP-003). پیش‌تر هر سه ViewModel همین ماشین وضعیت را
/// جدا جدا پیاده کرده بودند و معلوم نبود فیلتر «زنده» است یا «پیش‌نویس».
///
/// قرارداد: کنترل‌های فیلتر فقط Draft را عوض می‌کنند؛ هیچ چیز فیلتر نمی‌شود
/// تا وقتی Apply() صدا زده شود (دکمه «اعمال»). مقدار پیش‌فرض هر کلید «همه»
/// است، مگر اینکه هنگام خواندن پیش‌فرض دیگری داده شود (مثلا جستجو و بازه
/// مبلغ که پیش‌فرضشان رشته خالی است).
/// </summary>
public sealed class FilterState : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Dictionary<string, string> _draft = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _applied = new(StringComparer.Ordinal);

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void RaiseStateChanged()
    {
        Raise(nameof(HasPendingChanges));
        Raise(nameof(HasActiveFilters));
    }

    /// <summary>مقدار پیش‌نویس یک کلید (همان چیزی که در کنترل دیده می‌شود).</summary>
    public string GetDraft(string key, string defaultValue = "همه")
        => _draft.TryGetValue(key, out var v) ? v : defaultValue;

    /// <summary>مقدار اعمال‌شده یک کلید (همان چیزی که جدول واقعا با آن فیلتر شده).</summary>
    public string GetApplied(string key, string defaultValue = "همه")
        => _applied.TryGetValue(key, out var v) ? v : defaultValue;

    /// <summary>پیش‌نویس یک کلید را عوض می‌کند؛ فیلتر تا Apply اعمال نمی‌شود.</summary>
    public void SetDraft(string key, string? value)
    {
        var v = value ?? "";
        if (GetDraft(key) == v) return;
        _draft[key] = v;
        RaiseStateChanged();
    }

    /// <summary>همه پیش‌نویس‌ها را به‌عنوان فیلتر فعال قبول می‌کند.</summary>
    public void Apply()
    {
        foreach (var kv in _draft) _applied[kv.Key] = kv.Value;
        RaiseStateChanged();
    }

    /// <summary>پیش‌نویس و اعمال‌شده را برای این کلیدها به پیش‌فرض برمی‌گرداند.</summary>
    public void Clear(params (string Key, string Default)[] keys)
    {
        foreach (var (key, def) in keys)
        {
            _draft[key] = def;
            _applied[key] = def;
        }
        RaiseStateChanged();
    }

    /// <summary>همه کلیدهای شناخته‌شده را به پیش‌فرض برمی‌گرداند.</summary>
    public void ClearAll()
    {
        foreach (var key in _draft.Keys.ToList()) _draft[key] = DefaultFor(key);
        foreach (var key in _applied.Keys.ToList()) _applied[key] = DefaultFor(key);
        RaiseStateChanged();
    }

    /// <summary>پیش‌فرض یک کلید: «همه»، مگر کلیدهایی که مقدار خالی دارند.</summary>
    public static string DefaultFor(string key) => key is "Search" or "AmountFrom" or "AmountTo" ? "" : "همه";

    /// <summary>True وقتی پیش‌نویس با اعمال‌شده فرق دارد (یعنی «اعمال» هنوز زده نشده).</summary>
    public bool HasPendingChanges
    {
        get
        {
            var keys = _draft.Keys.Concat(_applied.Keys).Distinct(StringComparer.Ordinal);
            return keys.Any(k => GetDraft(k, DefaultFor(k)) != GetApplied(k, DefaultFor(k)));
        }
    }

    /// <summary>True وقتی حداقل یک فیلتر اعمال‌شده فعال است (غیر از پیش‌فرض).</summary>
    public bool HasActiveFilters
    {
        get
        {
            return _applied.Any(kv =>
            {
                var def = DefaultFor(kv.Key);
                return !string.IsNullOrWhiteSpace(kv.Value) && kv.Value != def;
            });
        }
    }
}
