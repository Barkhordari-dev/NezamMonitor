using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// جزییات یک دفتر (v12.7): با متن خام دفتر از پرونده باز می‌شود (مثل
/// «شماره 194 يزد»)؛ اول resolve می‌کند، بعد دفتر + اعضایش را نشان می‌دهد.
/// دفتر نباشد «یافت نشد» می‌گوید و دقیقاً می‌نویسد چه چیزی جستجو شد؛
/// جدول‌ها خالی باشند دلیل واقعی سید را می‌گوید. هیچ‌وقت کرش نمی‌کند و
/// مقدار خالی را «ثبت نشده» نشان می‌دهد.
/// </summary>
public sealed class DirectoryOfficeDetailViewModel : ViewModelBase
{
    private readonly DirectoryService _service;

    /// <summary>متن خام دفتر که از پرونده آمده (برای پیام «یافت نشد»).</summary>
    public string RawText { get; }

    private bool _isLoading = true;
    private string _message = "";
    private string _title = "";
    private string _name = "";
    private string _licence = "";
    private string _manager = "";
    private string _managerMobile = "";
    private string _contact = "";
    private string _address = "";
    private string _phone = "";
    private string _city = "";
    private string _province = "";
    private string _updatedAt = "";

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    /// <summary>پیام وسط صفحه وقتی جزییاتی برای نمایش نیست (یافت نشد / دلیل سید).</summary>
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
    public bool HasDetail { get; private set; }

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    public string Licence { get => _licence; private set => SetProperty(ref _licence, value); }
    public string Manager { get => _manager; private set => SetProperty(ref _manager, value); }
    public string ManagerMobile { get => _managerMobile; private set => SetProperty(ref _managerMobile, value); }
    public string Contact { get => _contact; private set => SetProperty(ref _contact, value); }
    public string Address { get => _address; private set => SetProperty(ref _address, value); }
    public string Phone { get => _phone; private set => SetProperty(ref _phone, value); }
    public string City { get => _city; private set => SetProperty(ref _city, value); }
    public string Province { get => _province; private set => SetProperty(ref _province, value); }
    public string UpdatedAt { get => _updatedAt; private set => SetProperty(ref _updatedAt, value); }

    public bool HasManagerMobile => !DirectoryService.IsMissing(ManagerMobile) && ManagerMobile != "ثبت نشده";
    public bool HasUpdatedAt => !string.IsNullOrWhiteSpace(UpdatedAt);

    /// <summary>اعضای دفتر؛ رشته همیشه از داده می‌آید، نه از کد.</summary>
    public ObservableCollection<DirectoryOfficeMemberItem> Members { get; } = new();

    public bool HasMembers => Members.Count > 0;

    public string MemberCountText =>
        $"اعضای دفتر ({DirectoryParser.ToPersianDigits(Members.Count.ToString())})";

    public DirectoryOfficeDetailViewModel(DirectoryService service, string rawOfficeText)
    {
        _service = service;
        RawText = rawOfficeText;
        Title = rawOfficeText;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Message = "";
        try
        {
            var office = await Task.Run(() => _service.ResolveOffice(RawText));
            if (office == null)
            {
                Message = _service.IsSeeded
                    ? _service.OfficeNotFoundMessage(RawText)
                    : _service.EmptyMessage("دفتر", RawText);
                return;
            }

            var detail = await Task.Run(() => _service.LoadOfficeDetail(office.SiteId));
            if (detail == null)
            {
                Message = _service.IsSeeded
                    ? _service.OfficeNotFoundMessage(RawText)
                    : _service.EmptyMessage("دفتر", RawText);
                return;
            }

            var o = detail.Office;
            Title = DirectoryService.Display(o.Name);
            Name = DirectoryService.Display(o.Name);
            Licence = DirectoryService.Display(o.Licence);
            Manager = DirectoryService.Display(o.Manager);
            ManagerMobile = DirectoryService.Display(o.Mobile);
            Contact = DirectoryService.Display(o.Contact);
            Address = DirectoryService.Display(o.Address);
            Phone = DirectoryService.Display(o.Phone);
            City = DirectoryService.Display(o.City);
            Province = DirectoryService.Display(o.Province);
            UpdatedAt = string.IsNullOrWhiteSpace(o.UpdatedAt) ? "" : o.UpdatedAt;
            OnPropertyChanged(nameof(HasManagerMobile));
            OnPropertyChanged(nameof(HasUpdatedAt));

            Members.Clear();
            foreach (var m in detail.Members)
            {
                Members.Add(new DirectoryOfficeMemberItem
                {
                    SiteId = m.SiteId,
                    FullName = $"{m.Name} {m.Family}".Trim(),
                    Discipline = DirectoryService.Display(m.Discipline),
                    Phone = DirectoryService.Display(m.Phone)
                });
            }
            OnPropertyChanged(nameof(HasMembers));
            OnPropertyChanged(nameof(MemberCountText));

            HasDetail = true;
            OnPropertyChanged(nameof(HasDetail));
        }
        catch
        {
            // خواندن دفترچه هیچ‌وقت نباید پنجره را ببندد یا کرش کند.
            if (!HasDetail && string.IsNullOrEmpty(Message))
                Message = "خطا در خواندن اطلاعات دفتر.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>یک عضو دفتر در لیست اعضا: نام کامل + رشته + همراه.</summary>
public sealed class DirectoryOfficeMemberItem
{
    public int SiteId { get; set; }
    public string FullName { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string Phone { get; set; } = "";
}
