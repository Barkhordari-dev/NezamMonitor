namespace NezamMonitorPro.App.Services;

/// <summary>
/// ویوهایی که در حافظه کش می‌شوند و هنگام بازگشت به آن‌ها باید داده‌شان تازه‌سازی شود.
/// (NavigationService به‌جای ساخت دوباره ویو، نمونه موجود را نمایش می‌دهد و این متد را صدا می‌زند.)
/// </summary>
public interface INavigationAware
{
    void OnNavigatedTo();
}
