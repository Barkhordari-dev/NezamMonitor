using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// دفترچه دفاتر و مهندسین یزد (v12.7): اسنپ‌شات فقط‌خواندنی که با برنامه
/// می‌آید (۲۰۳ دفتر، ۵٬۰۷۹ مهندس)؛ هیچ به‌روزرسانی درون‌برنامه‌ای ندارد
/// (تصمیم کاربر ۲۰۲۶-۱۱-۲۰). یک نمونه singleton در DI است و همیشه روی همان
/// اتصال DatabaseService.Instance کار می‌کند؛ اتصال دوم باز نمی‌کند.
/// وضعیت سید (نسخه/وضعیت/تعدادها/دلیل خطا) اینجا نگه داشته می‌شود تا کارت
/// تشخیص در تنظیمات و صفحه‌های جزییات همان چیز را نشان بدهند.
/// </summary>
public sealed class DirectoryService : INotifyPropertyChanged
{
    private readonly NezamDatabase _db;
    private readonly DirectoryStore _store;

    /// <summary>Desktop User-Agent برای صفحه جزییات مهندس (فقط عکس).</summary>
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private static readonly HttpClient PhotoHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private string _version = "";
    private string _state = "unknown";
    private int _officeCount;
    private int _engineerCount;
    private string _reason = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public DirectoryService(NezamDatabase db)
    {
        _db = db;
        _store = new DirectoryStore(db);
    }

    /// <summary>نسخه اسنپ‌شات سیدشده (meta.generated)، خالی وقتی هنوز سید نشده.</summary>
    public string Version
    {
        get => _version;
        private set => SetField(ref _version, value);
    }

    /// <summary>وضعیت سید: unknown | seeding | seeded | already | failed.</summary>
    public string State
    {
        get => _state;
        private set
        {
            if (!SetField(ref _state, value)) return;
            OnPropertyChanged(nameof(IsSeeded));
            OnPropertyChanged(nameof(StateText));
        }
    }

    public int OfficeCount
    {
        get => _officeCount;
        private set
        {
            if (!SetField(ref _officeCount, value)) return;
            OnPropertyChanged(nameof(OfficeCountText));
        }
    }

    public int EngineerCount
    {
        get => _engineerCount;
        private set
        {
            if (!SetField(ref _engineerCount, value)) return;
            OnPropertyChanged(nameof(EngineerCountText));
        }
    }

    /// <summary>دلیل خطا وقتی State برابر failed است؛ بقیه وقت‌ها خالی.</summary>
    public string Reason
    {
        get => _reason;
        private set => SetField(ref _reason, value);
    }

    public bool IsSeeded => State is "seeded" or "already";

    /// <summary>وضعیت سید به زبان ساده برای کارت تنظیمات.</summary>
    public string StateText => State switch
    {
        "seeding" => "در حال بارگذاری…",
        "seeded" => "بارگذاری شد",
        "already" => "از قبل بارگذاری شده",
        "failed" => "بارگذاری نشد",
        _ => "نامشخص"
    };

    public string VersionText => string.IsNullOrEmpty(Version) ? "—" : Version;

    public string OfficeCountText => DirectoryParser.ToPersianDigits(OfficeCount.ToString());

    public string EngineerCountText => DirectoryParser.ToPersianDigits(EngineerCount.ToString());

    /// <summary>
    /// اسنپ‌شات را اگر لازم است سید می‌کند؛ روی نخ پس‌زمینه اجرا می‌شود،
    /// هیچ‌وقت خطا نمی‌دهد و وضعیت را برای کارت تنظیمات به‌روز می‌کند.
    /// </summary>
    public async Task EnsureSeededAsync()
    {
        if (IsSeeded)
        {
            RefreshCounts();
            return;
        }
        if (State == "seeding") return;

        State = "seeding";
        Reason = "";
        try
        {
            var outcome = await Task.Run(() => DirectorySnapshotSeeder.EnsureSeeded(_db));
            switch (outcome)
            {
                case DirectorySnapshotSeeder.SeedOutcome.Seeded seeded:
                    Version = seeded.Version ?? "";
                    State = "seeded";
                    break;
                case DirectorySnapshotSeeder.SeedOutcome.AlreadySeeded:
                    Version = ReadStoredVersion();
                    State = "already";
                    break;
                case DirectorySnapshotSeeder.SeedOutcome.Failed failed:
                    Version = ReadStoredVersion();
                    Reason = failed.Reason ?? "";
                    State = "failed";
                    break;
                default:
                    Version = ReadStoredVersion();
                    Reason = "نتیجه سید نامشخص بود.";
                    State = "failed";
                    break;
            }
        }
        catch (Exception ex)
        {
            Reason = ex.Message;
            State = "failed";
        }
        RefreshCounts();
    }

    /// <summary>تعدادهای زنده را از جدول‌ها می‌خواند؛ خطا را بی‌صدا رد می‌کند.</summary>
    public void RefreshCounts()
    {
        try { OfficeCount = _store.GetOfficeCount(); } catch { }
        try { EngineerCount = _store.GetEngineerCount(); } catch { }
    }

    private string ReadStoredVersion()
    {
        try { return _db.GetSetting(SettingKeys.DirectorySnapshotVersion) ?? ""; }
        catch { return ""; }
    }

    /// <summary>متن خام دفتر از پرونده (مثل «شماره 194 يزد») را به رکورد دفتر می‌رساند؛ پیدا نشد = null.</summary>
    public DirectoryOffice? ResolveOffice(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return DirectoryParser.ResolveOffice(raw, _store.GetAllOffices()); }
        catch { return null; }
    }

    /// <summary>نام کامل مهندس را به رکورد مهندس می‌رساند؛ پیدا نشد = null.
    /// تطبیق فقط روی نام و نام خانوادگی است، پس اول یک فهرست سبک از همان
    /// سه ستون خوانده می‌شود و رکورد کامل بعداً از store گرفته می‌شود.</summary>
    public DirectoryEngineer? ResolveEngineer(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            var match = DirectoryParser.ResolveEngineer(raw, LoadEngineerNameIndex());
            return match == null ? null : _store.GetEngineer(match.SiteId);
        }
        catch { return null; }
    }

    /// <summary>
    /// فهرست سبک (SiteId, Name, Family) همه مهندس‌ها برای تطبیق نام.
    /// DirectoryStore متد GetAllEngineers ندارد، پس همین‌جا — روی همان اتصال
    /// NezamDatabase و زیر همان قفل SyncRoot — فقط سه ستون لازم خوانده
    /// می‌شود؛ اتصال دومی باز نمی‌شود و به Core دست نمی‌خورد.
    /// </summary>
    private List<DirectoryEngineer> LoadEngineerNameIndex()
    {
        var list = new List<DirectoryEngineer>();
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT SiteId, Name, Family FROM directory_engineers";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DirectoryEngineer(
                    SiteId: r.IsDBNull(0) ? 0 : r.GetInt32(0),
                    Name: r.IsDBNull(1) ? "" : r.GetString(1),
                    Family: r.IsDBNull(2) ? "" : r.GetString(2)));
            }
        }
        return list;
    }

    /// <summary>دفتر + اعضای آن؛ دفتر نباشد = null.</summary>
    public DirectoryOfficeDetail? LoadOfficeDetail(int siteId)
    {
        try
        {
            var office = _store.GetOffice(siteId);
            if (office == null) return null;
            return new DirectoryOfficeDetail(office, _store.GetEngineersForOffice(siteId));
        }
        catch { return null; }
    }

    /// <summary>مهندس + مدارک تحصیلی + تاریخ اخذ پروانه؛ مهندس نباشد = null.</summary>
    public DirectoryEngineerProfile? LoadEngineerProfile(int siteId)
    {
        try
        {
            var engineer = _store.GetEngineer(siteId);
            if (engineer == null) return null;
            return new DirectoryEngineerProfile(engineer, _store.GetEducation(siteId), _store.GetLicenceHistory(siteId));
        }
        catch { return null; }
    }

    /// <summary>
    /// پیام «اطلاعات بارگذاری نشده» با دلیل واقعی (مثل اندروید): وقتی جدول‌ها
    /// خالی‌اند، به‌جای «یافت نشد» خشک، می‌گوید سید خراب شده یا هنوز در حال
    /// بارگذاری است.
    /// </summary>
    public string EmptyMessage(string what, string searched) => State switch
    {
        "failed" => $"اطلاعات {what} بارگذاری نشده است: {Reason} (جستجو: «{searched}»)",
        "seeding" => $"اطلاعات {what} هنوز در حال بارگذاری است — چند ثانیه دیگر دوباره تلاش کنید (جستجو: «{searched}»)",
        _ => $"اطلاعات {what} در برنامه نیست (جستجو: «{searched}»)"
    };

    /// <summary>«دفتر «X» (جستجو: دفتر 194) یافت نشد» — مثل اندروید.</summary>
    public string OfficeNotFoundMessage(string raw)
    {
        var wanted = "";
        try { wanted = DirectoryParser.OfficeQueryName(raw) ?? ""; } catch { }
        return !string.IsNullOrWhiteSpace(wanted) && wanted != raw
            ? $"دفتر «{raw}» (جستجو: {wanted}) یافت نشد"
            : $"دفتر «{raw}» یافت نشد";
    }

    public string EngineerNotFoundMessage(string name) => $"مهندس «{name}» یافت نشد";

    /// <summary>
    /// عکس مهندس از صفحه جزییات خودش (یک GET). فقط برای نمایش است و هیچ‌وقت
    /// در دیتابیس ذخیره نمی‌شود؛ آفلاین/خطا/بی‌عکس = null، بی‌صدا.
    /// </summary>
    public async Task<string?> FetchPhotoUrlAsync(int siteId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{DirectoryParser.BaseUrl}/resume/engineers/{siteId}");
            request.Headers.UserAgent.ParseAdd(DesktopUserAgent);
            request.Headers.ConnectionClose = true;
            request.Headers.AcceptEncoding.ParseAdd("identity");
            using var response = await PhotoHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var html = await response.Content.ReadAsStringAsync();
            var url = DirectoryParser.ParseEngineerPhotoFromHtml(html);
            if (string.IsNullOrWhiteSpace(url) || url.Contains("images.png")) return null;
            return url;
        }
        catch { return null; }
    }

    /// <summary>مقدار نمایشی یک فیلد: خالی/«_»/«-» می‌شود «ثبت نشده».</summary>
    public static string Display(string? value) => DirectoryParser.DirectoryDisplayValue(value ?? "");

    /// <summary>آیا این مقدار یعنی «چیزی ثبت نشده» (خالی یا «_» یا «-»).</summary>
    public static bool IsMissing(string? value) => DirectoryParser.IsMissingDirectoryValue(value ?? "");

    /// <summary>
    /// بایت‌های عکس مهندس از یک URL (اسنپ‌شات یا صفحه جزئیات). مثل اندروید
    /// (ResilientHttpClient): هر تلاش یک درخواست تازه با Connection: close و
    /// بدون فشرده‌سازی است؛ تا ۳ بار تلاش می‌کند. ناموفق = null + یک خط در
    /// لاگ خطا (دیگر بی‌صدا نیست — ولی هیچ پنجره‌ای باز نمی‌شود).
    /// چرا بایت، نه خودِ URL؟ بارگذاری مستقیم URL در WPF (BitmapImage)
    /// به سرور ناپایدار دفترچه (قطع‌شدن وسط دانلود، همان «unexpected end
    /// of stream» اندروید 3.11.2) باخت و بی‌هیچ دلیلی عکس را پنهان می‌کرد؛
    /// اینجا خودمان با همان کلاینت مقاوم دانلود می‌کنیم.
    /// </summary>
    public async Task<byte[]?> FetchPhotoBytesAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        Exception? lastError = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(DesktopUserAgent);
                request.Headers.ConnectionClose = true;
                request.Headers.AcceptEncoding.ParseAdd("identity");
                using var response = await PhotoHttp.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    lastError = new HttpRequestException($"HTTP {(int)response.StatusCode}");
                    continue;
                }
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType != null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    lastError = new HttpRequestException($"unexpected content type {mediaType}");
                    continue;
                }
                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0)
                {
                    lastError = new HttpRequestException("empty photo body");
                    continue;
                }
                return bytes;
            }
            catch (Exception ex) { lastError = ex; }
        }
        ErrorLog.Write(lastError ?? new HttpRequestException("photo download failed"),
            $"DirectoryService.FetchPhotoBytesAsync url={url}");
        return null;
    }

    /// <summary>عکس ذخیره‌شده در اسنپ‌شات؛ خالی یا images.png یعنی «عکس ندارد».</summary>
    public static string? StoredPhotoUrl(string? photo)
    {
        if (string.IsNullOrWhiteSpace(photo) || photo.Contains("images.png")) return null;
        if (photo.StartsWith("/")) return DirectoryParser.BaseUrl + photo;
        return photo;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        // وضعیت سید روی نخ پس‌زمینه عوض می‌شود؛ اعلان را به نخ UI می‌فرستیم
        // تا بایندینگ کارت تنظیمات از نخ اشتباه خبر نگیرد.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name))));
            return;
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>یک دفتر با اعضای آن، برای پنجره جزییات دفتر.</summary>
public sealed record DirectoryOfficeDetail(DirectoryOffice Office, IReadOnlyList<DirectoryEngineer> Members);

/// <summary>یک مهندس با مدارک تحصیلی و تاریخ اخذ پروانه، برای پنجره پروفایل.</summary>
public sealed record DirectoryEngineerProfile(
    DirectoryEngineer Engineer,
    IReadOnlyList<DirectoryEducation> Education,
    IReadOnlyList<DirectoryLicenceHistory> History);
