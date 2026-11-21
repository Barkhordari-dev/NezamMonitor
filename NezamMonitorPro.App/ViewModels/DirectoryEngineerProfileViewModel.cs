using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// پروفایل یک مهندس (v12.7): با نام کامل باز می‌شود. هر چهار کارت همیشه به
/// همان ترتیب و برای همه مهندس‌ها — حتی مهندس‌های فقط‌لیستی — نشان داده
/// می‌شوند و هیچ کارتی غیب نمی‌شود؛ جای خالی «ثبت نشده» محو نوشته می‌شود.
/// عکس اول از اسنپ‌شات می‌آید و یک بار تازه‌اش از صفحه مهندس گرفته می‌شود
/// (فقط برای نمایش، هیچ‌وقت ذخیره نمی‌شود)؛ نباشد حروف اول نام می‌آید.
/// </summary>
public sealed class DirectoryEngineerProfileViewModel : ViewModelBase
{
    private readonly DirectoryService _service;

    /// <summary>نامی که جستجو شد (برای پیام «یافت نشد»).</summary>
    public string SearchedName { get; }

    private int _siteId;
    private bool _isLoading = true;
    private string _message = "";
    private string _fullName = "";
    private string _initials = "؟";
    private string _discipline = "";
    private string _officeName = "";
    private ImageSource? _photoImage;
    private string? _photoUrl;
    private string _designGrade = "";
    private string _supervisionGrade = "";
    private string _executionGrade = "";
    private string _licenceIssue = "";
    private string _licenceValidity = "";
    private string _phone = "";
    private string _updatedAt = "";

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    /// <summary>پیام وسط صفحه وقتی پروفایلی برای نمایش نیست (یافت نشد / دلیل سید).</summary>
    public string Message
    {
        get => _message;
        private set
        {
            if (!SetProperty(ref _message, value)) return;
            OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool HasProfile { get; private set; }

    // ── کارت ۱: سربرگ ──
    public string FullName { get => _fullName; private set => SetProperty(ref _fullName, value); }
    public string Initials { get => _initials; private set => SetProperty(ref _initials, value); }

    /// <summary>رشته از داده می‌آید، هیچ‌وقت از کد؛ خالی = «ثبت نشده».</summary>
    public string Discipline { get => _discipline; private set => SetProperty(ref _discipline, value); }

    public string OfficeName { get => _officeName; private set => SetProperty(ref _officeName, value); }
    public bool HasOffice => !string.IsNullOrWhiteSpace(OfficeName) && OfficeName != "ثبت نشده";

    /// <summary>عکس پروفایل، آماده برای نمایش — بایت‌های دانلودشده، نه URL خام.
    /// WPF با بارگذاری مستقیم URL از سرور ناپایدار دفترچه باخت و بی‌هیچ
    /// دلیلی عکس را پنهان می‌کرد؛ اینجا بایت‌ها با کلاینت مقاوم دانلود
    /// و از حافظه ساخته می‌شوند. null یعنی «عکس ندارد» → حروف اول نام.</summary>
    public ImageSource? PhotoImage
    {
        get => _photoImage;
        private set
        {
            if (!SetProperty(ref _photoImage, value)) return;
            OnPropertyChanged(nameof(HasPhoto));
        }
    }

    public bool HasPhoto => PhotoImage != null;

    // ── کارت ۲: مشخصات پروانه ──
    public string DesignGrade { get => _designGrade; private set => SetProperty(ref _designGrade, value); }
    public string SupervisionGrade { get => _supervisionGrade; private set => SetProperty(ref _supervisionGrade, value); }
    public string ExecutionGrade { get => _executionGrade; private set => SetProperty(ref _executionGrade, value); }
    public string LicenceIssue { get => _licenceIssue; private set => SetProperty(ref _licenceIssue, value); }
    public string LicenceValidity { get => _licenceValidity; private set => SetProperty(ref _licenceValidity, value); }
    public string Phone { get => _phone; private set => SetProperty(ref _phone, value); }
    public string UpdatedAt { get => _updatedAt; private set => SetProperty(ref _updatedAt, value); }

    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone) && Phone != "ثبت نشده";
    public bool HasUpdatedAt => !string.IsNullOrWhiteSpace(UpdatedAt);

    // ── کارت ۳: تاریخ اخذ پروانه ──
    public ObservableCollection<DirectoryHistoryRow> HistoryRows { get; } = new();
    public bool HasHistory => HistoryRows.Count > 0;

    // ── کارت ۴: مدارک تحصیلی ──
    public ObservableCollection<DirectoryEducationRow> EducationRows { get; } = new();
    public bool HasEducation => EducationRows.Count > 0;

    public DirectoryEngineerProfileViewModel(DirectoryService service, string engineerName)
    {
        _service = service;
        SearchedName = engineerName;
        FullName = engineerName;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Message = "";
        try
        {
            var engineer = await Task.Run(() => _service.ResolveEngineer(SearchedName));
            if (engineer == null)
            {
                Message = _service.IsSeeded
                    ? _service.EngineerNotFoundMessage(SearchedName)
                    : _service.EmptyMessage("مهندس", SearchedName);
                return;
            }

            var profile = await Task.Run(() => _service.LoadEngineerProfile(engineer.SiteId));
            if (profile == null)
            {
                Message = _service.IsSeeded
                    ? _service.EngineerNotFoundMessage(SearchedName)
                    : _service.EmptyMessage("مهندس", SearchedName);
                return;
            }

            var e = profile.Engineer;
            _siteId = e.SiteId;
            FullName = $"{e.Name} {e.Family}".Trim();
            Initials = MakeInitials(FullName);
            Discipline = DirectoryService.Display(e.Discipline);
            OfficeName = DirectoryService.Display(e.OfficeName);
            DesignGrade = DirectoryService.Display(e.DesignGrade);
            SupervisionGrade = DirectoryService.Display(e.SupervisionGrade);
            ExecutionGrade = DirectoryService.Display(e.ExecutionGrade);
            LicenceIssue = DirectoryService.Display(e.LicenceIssue);
            LicenceValidity = DirectoryService.Display(e.LicenceValidity);
            Phone = DirectoryService.Display(e.Phone);
            UpdatedAt = string.IsNullOrWhiteSpace(e.UpdatedAt) ? "" : e.UpdatedAt;
            OnPropertyChanged(nameof(HasOffice));
            OnPropertyChanged(nameof(HasPhone));
            OnPropertyChanged(nameof(HasUpdatedAt));

            // عکس ذخیره‌شده اول نشان داده می‌شود؛ تازه‌اش بعداً جایش را می‌گیرد.
            // دانلود بایت‌ها در پس‌زمینه است تا متن پروفایل معطل عکس نماند.
            _photoUrl = DirectoryService.StoredPhotoUrl(e.Photo);
            _ = LoadStoredPhotoAsync(_siteId, _photoUrl);

            HistoryRows.Clear();
            foreach (var h in profile.History)
            {
                var lines = new List<string>();
                AddGradeLine(lines, "پایه ۱", DirectoryParser.LicenceGradeDate(h.Grade1));
                AddGradeLine(lines, "پایه ۲", DirectoryParser.LicenceGradeDate(h.Grade2));
                AddGradeLine(lines, "پایه ۳", DirectoryParser.LicenceGradeDate(h.Grade3));
                HistoryRows.Add(new DirectoryHistoryRow
                {
                    Kind = DirectoryService.Display(h.Kind),
                    GradeLines = lines
                });
            }
            OnPropertyChanged(nameof(HasHistory));

            EducationRows.Clear();
            foreach (var edu in profile.Education)
            {
                var degreeLine = $"{edu.Degree} {edu.Field}".Trim();
                var university = $"{edu.UniversityType} {edu.University}".Trim();
                var place = JoinNonEmpty("، ", edu.City, edu.Country);
                var universityLine = university;
                if (!string.IsNullOrEmpty(place))
                    universityLine = string.IsNullOrEmpty(universityLine) ? place : $"{universityLine} — {place}";
                EducationRows.Add(new DirectoryEducationRow
                {
                    DegreeLine = string.IsNullOrEmpty(degreeLine) ? "ثبت نشده" : degreeLine,
                    Orientation = string.IsNullOrWhiteSpace(edu.Orientation) ? "" : edu.Orientation,
                    UniversityLine = string.IsNullOrEmpty(universityLine) ? "ثبت نشده" : universityLine,
                    DateText = DirectoryParser.EducationDateText(edu.Start, edu.End)
                });
            }
            OnPropertyChanged(nameof(HasEducation));

            HasProfile = true;
            OnPropertyChanged(nameof(HasProfile));

            // یک بار عکس تازه؛ اگر کاربر تا آن موقع پروفایل دیگری باز کرده باشد، اعمال نمی‌شود.
            _ = RefreshPhotoAsync(_siteId);
        }
        catch
        {
            if (!HasProfile && string.IsNullOrEmpty(Message))
                Message = "خطا در خواندن اطلاعات مهندس.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>وقتی عکس بارگذاری نشد (ImageFailed)، حروف اول نام می‌ماند.</summary>
    public void HidePhoto() => PhotoImage = null;

    /// <summary>دانلود عکس ذخیره‌شده و نشان‌دادنش؛ نشد حروف اول نام می‌ماند.</summary>
    private async Task LoadStoredPhotoAsync(int siteId, string? url)
    {
        var image = await LoadPhotoImageAsync(url);
        if (image != null && _siteId == siteId)
            PhotoImage = image;
    }

    /// <summary>بایت‌های یک عکس را می‌گیرد و BitmapImage آماده نمایش می‌سازد؛
    /// نشد null برمی‌گرداند (دلیلش را سرویس در لاگ خطا نوشته است).</summary>
    private async Task<ImageSource?> LoadPhotoImageAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var bytes = await _service.FetchPhotoBytesAsync(url);
        if (bytes == null || bytes.Length == 0) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (System.Exception ex)
        {
            NezamMonitorPro.App.Services.ErrorLog.Write(ex, "DirectoryEngineerProfileViewModel.LoadPhotoImageAsync");
            return null;
        }
    }

    private async Task RefreshPhotoAsync(int siteId)
    {
        var fresh = await _service.FetchPhotoUrlAsync(siteId);
        if (fresh == null || fresh == _photoUrl) return;
        if (_siteId != siteId || !HasProfile) return;
        var image = await LoadPhotoImageAsync(fresh);
        if (image != null && _siteId == siteId)
        {
            _photoUrl = fresh;
            PhotoImage = image;
        }
    }

    private static void AddGradeLine(List<string> lines, string label, string? date)
    {
        if (date == null) return;
        lines.Add($"{label}: {DirectoryParser.ToPersianDigits(date)}");
    }

    private static string JoinNonEmpty(string separator, params string[] parts)
    {
        var kept = new List<string>();
        foreach (var p in parts)
        {
            var t = (p ?? "").Trim();
            if (t.Length > 0) kept.Add(t);
        }
        return string.Join(separator, kept);
    }

    /// <summary>حروف اول دو کلمه اول نام؛ هیچ کلمه‌ای نباشد «؟».</summary>
    private static string MakeInitials(string fullName)
    {
        var words = (fullName ?? "").Split(new[] { ' ', '‌' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "؟";
        var first = words[0].Substring(0, 1);
        var second = words.Length > 1 ? words[1].Substring(0, 1) : "";
        return first + second;
    }
}

/// <summary>یک ردیف «تاریخ اخذ پروانه»: نوع پروانه + فقط پایه‌های گرفته‌شده.</summary>
public sealed class DirectoryHistoryRow
{
    public string Kind { get; set; } = "";
    public List<string> GradeLines { get; set; } = new();

    /// <summary>هیچ پایه‌ای گرفته نشده = ردیف «ثبت نشده» نشان می‌دهد.</summary>
    public bool HasGrades => GradeLines.Count > 0;
}

/// <summary>یک ردیف «مدارک تحصیلی».</summary>
public sealed class DirectoryEducationRow
{
    public string DegreeLine { get; set; } = "";
    public string Orientation { get; set; } = "";
    public bool HasOrientation => !string.IsNullOrWhiteSpace(Orientation);
    public string UniversityLine { get; set; } = "";
    public string DateText { get; set; } = "";
}
