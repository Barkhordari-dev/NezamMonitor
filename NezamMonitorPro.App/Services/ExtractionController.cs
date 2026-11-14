using System.IO;
using NezamMonitorPro.App.ViewModels;
using System.Threading;
using System.Windows.Input;
using NezamMonitorPro.Core.Api;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// Extraction controller — API-first with Playwright fallback for Reports.
/// </summary>
public sealed class ExtractionController : ViewModelBase
{
    private static ExtractionController? _instance;
    private static readonly object _lock = new();

    private bool _isBusy;
    private string _statusMessage = "آماده";
    private double _progress;
    private string _logText = "";
    private CancellationTokenSource? _cts;

    // ── Friendly (non-technical) status for the UI ──────────────────────
    public enum UpdateStage
    {
        Idle,
        Connecting,
        Receiving,
        Saving,
        Downloading,
        Success,
        Stopped,
        Error
    }

    private UpdateStage _stage = UpdateStage.Idle;
    /// <summary>Current user-facing stage of the sync operation.</summary>
    public UpdateStage Stage { get => _stage; set => SetProperty(ref _stage, value); }

    private string _friendlyStatus = "آماده برای شروع";
    /// <summary>Friendly, non-technical message shown in the status card.</summary>
    public string FriendlyStatus { get => _friendlyStatus; set => SetProperty(ref _friendlyStatus, value); }

    private int _processedCount;
    /// <summary>Number of items processed so far (X of Y).</summary>
    public int ProcessedCount { get => _processedCount; set => SetProperty(ref _processedCount, value); }

    private int _totalCount;
    /// <summary>Total number of items to process (Y).</summary>
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

    private string _statusDetail = "";
    /// <summary>Optional secondary line (still human readable).</summary>
    public string StatusDetail { get => _statusDetail; set => SetProperty(ref _statusDetail, value); }

    /// <summary>True when the last run finished successfully.</summary>
    public bool IsSuccess { get => _stage == UpdateStage.Success; }

    /// <summary>Progress text like "12 از 40" (empty when there is nothing to count).</summary>
    public string ProgressCountText =>
        _totalCount > 0 ? $"{_processedCount} از {_totalCount}" : "";

    /// <summary>Sets the friendly status and raises the dependent properties.</summary>
    private void SetFriendly(UpdateStage stage, string message, int processed = 0, int total = 0, string detail = "")
    {
        _stage = stage;
        _friendlyStatus = message;
        _processedCount = processed;
        _totalCount = total;
        _statusDetail = detail;
        OnPropertyChanged(nameof(Stage));
        OnPropertyChanged(nameof(FriendlyStatus));
        OnPropertyChanged(nameof(ProcessedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(StatusDetail));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(ProgressCountText));
    }

    public static ExtractionController Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                    _instance ??= new ExtractionController();
            return _instance;
        }
    }

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }
    private int _maxCases;
    public int MaxCases { get => _maxCases; set => SetProperty(ref _maxCases, value); }
    private bool _filterIncompleteOnly;
    public bool FilterIncompleteOnly { get => _filterIncompleteOnly; set => SetProperty(ref _filterIncompleteOnly, value); }
    private bool _extractEngineers = true;
    public bool ExtractEngineers { get => _extractEngineers; set => SetProperty(ref _extractEngineers, value); }
    private bool _extractFees = true;
    public bool ExtractFees { get => _extractFees; set => SetProperty(ref _extractFees, value); }
    private bool _extractSpecifications = true;
    public bool ExtractSpecifications { get => _extractSpecifications; set => SetProperty(ref _extractSpecifications, value); }
    private bool _extractReports = true;
    public bool ExtractReports { get => _extractReports; set => SetProperty(ref _extractReports, value); }
    private bool _extractReportFiles = false;
    public bool ExtractReportFiles { get => _extractReportFiles; set => SetProperty(ref _extractReportFiles, value); }
    private bool _skipExistingReportFiles = true;
    public bool SkipExistingReportFiles { get => _skipExistingReportFiles; set => SetProperty(ref _skipExistingReportFiles, value); }
    private string _outputPath = "";
        public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
        private string _username = "";
        public string Username { get => _username; set => SetProperty(ref _username, value); }
        private string _password = "";
        public string Password { get => _password; set => SetProperty(ref _password, value); }
        private bool _rememberMe = false;
        public bool RememberMe { get => _rememberMe; set => SetProperty(ref _rememberMe, value); }
    private bool _updateAllSpecifications = true;
    public bool UpdateAllSpecifications { get => _updateAllSpecifications; set => SetProperty(ref _updateAllSpecifications, value); }
    private int _startFromIndex = 1;
    public int StartFromIndex { get => _startFromIndex; set => SetProperty(ref _startFromIndex, value); }

    private string _userDisplayName = "";
    /// <summary>Logged-in user's full name (known after an update; empty before).</summary>
    public string UserDisplayName
    {
        get => _userDisplayName;
        set { if (SetProperty(ref _userDisplayName, value)) OnPropertyChanged(nameof(WelcomeMessage)); }
    }
    /// <summary>Personal welcome — empty until the user's name is known.</summary>
    public string WelcomeMessage => string.IsNullOrWhiteSpace(UserDisplayName)
        ? "" : $"سلام {UserDisplayName}، خوش آمدی 👋";

    /// <summary>
    /// نتیجه استخراج از API؛ LoginFailed یعنی ورود ناموفق بود
    /// (نه اینکه پرونده‌ای نبود) — S-001.
    /// </summary>
    public sealed class ExtractionResult
    {
        public List<Case> Cases { get; set; } = new();
        public bool LoginFailed { get; set; }
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand LoadLatestCommand { get; }

    private ExtractionController()
        {
            StartCommand = new AsyncRelayCommand(StartExtractionAsync, () => !IsBusy);
            StopCommand = new RelayCommand(StopExtraction, () => IsBusy);
            LoadLatestCommand = new RelayCommand(LoadLatest);

            // Default output path
            var db = DatabaseService.Instance;
            _outputPath = db.GetSetting(SettingKeys.OutputPath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "outputs");
            _userDisplayName = db.GetSetting(SettingKeys.ApiUserName) ?? "";
        }

    public void AppendLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    private async Task StartExtractionAsync()
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();

        try
        {
            var db = DatabaseService.Instance;

            // Use credentials from UI (Username/Password properties)
            // Fall back to database if UI is empty
            var username = Username;
            var password = Password;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                var settings = db.LoadSettings();
                if (string.IsNullOrEmpty(username) && settings.TryGetValue(SettingKeys.Username, out var u)) username = u;
                // SEC-006: رمز ذخیره‌شده فقط وقتی خوانده می‌شود که «مرا به خاطر بسپار» روشن باشد.
                if (string.IsNullOrEmpty(password) && settings.TryGetValue(SettingKeys.RememberMe, out var rm)
                    && rm == "true" && settings.TryGetValue(SettingKeys.Password, out var p)) password = p;
            }

            // Save credentials for next time if RememberMe is checked
            if (RememberMe && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                db.SaveSetting(SettingKeys.Username, username);
                db.SaveSetting(SettingKeys.Password, password);
                db.SaveSetting(SettingKeys.RememberMe, "true");
            }
            else if (!RememberMe)
            {
                // SEC-006: وقتی «مرا به خاطر بسپار» خاموش است، رمز ذخیره‌شده پاک می‌شود.
                try { UpdateViewModel.DeleteSetting(db, SettingKeys.Password); db.SaveSetting(SettingKeys.RememberMe, "false"); } catch { }
            }

            AppendLog("شروع استخراج API...");
            SetFriendly(UpdateStage.Connecting, "در حال اتصال به سرور...");

            var startTime = DateTime.Now;
            var result = await ExtractViaApiAsync(username, password, db);
            var cases = result.Cases;

            // Download report files
            if (cases.Count > 0 && ExtractReportFiles)
            {
                AppendLog("دانلود فایل‌های گزارش‌ها...");
                SetFriendly(UpdateStage.Downloading, "در حال دریافت فایل‌های پیوست...",
                    0, cases.Count);
                await DownloadReportFilesAsync(cases, username, password);
            }

            var elapsed = DateTime.Now - startTime;
            AppendLog($"تکمیل: {cases.Count} پرونده در {elapsed.TotalSeconds:F0} ثانیه");

            if (cases.Count > 0)
            {
                AppendLog("ذخیره در دیتابیس...");
                StatusMessage = "ذخیره اسنپ‌شات...";
                SetFriendly(UpdateStage.Saving, "در حال ذخیره‌سازی اطلاعات...",
                    cases.Count, cases.Count);
                // Run DB save on background thread to avoid UI freeze
                await Task.Run(() =>
                {
                    var snapshotId = db.CreateSnapshot(startTime);
                    db.SaveSnapshot(snapshotId, cases);
                    db.FinalizeSnapshot(snapshotId, cases.Count);
                    db.SetActiveSnapshot(snapshotId);
                    // Hidden menu: permanent overrides win over the fresh server values.
                    // ARCH-003: دسترسی مستقیم به اتصال مشترک زیر قفل اتصال انجام می‌شود.
                    try { lock (db.SyncRoot) { HiddenOverrideApplier.ApplyAll(db.Connection); } } catch { }
                });
                AppendLog($"ذخیره شد ✓ — {cases.Count} پرونده فعال شد");
            }
            else if (!result.LoginFailed)
            {
                AppendLog("پرونده‌ای استخراج نشد");
            }

            StatusMessage = $"تکمیل — {cases.Count} پرونده";
            if (cases.Count > 0)
                SetFriendly(UpdateStage.Success, "عملیات با موفقیت انجام شد.",
                    cases.Count, cases.Count,
                    $"{cases.Count} پرونده در {elapsed.TotalSeconds:F0} ثانیه دریافت و ذخیره شد");
            else if (result.LoginFailed)
                // S-001: پیام «ورود ناموفق بود» دیگر با «اطلاعاتی برای دریافت یافت نشد» بازنویسی نمی‌شود.
                SetFriendly(UpdateStage.Error, "ورود ناموفق بود. اطلاعات حساب را بررسی کنید.");
            else
                SetFriendly(UpdateStage.Error, "اطلاعاتی برای دریافت یافت نشد.");
        }
        catch (OperationCanceledException)
        {
            AppendLog("استخراج متوقف شد.");
            StatusMessage = "متوقف شد";
            SetFriendly(UpdateStage.Stopped, "عملیات توسط کاربر متوقف شد.");
        }
        catch (Exception ex)
        {
            AppendLog($"خطا: {ex.Message}");
            StatusMessage = "خطا";
            SetFriendly(UpdateStage.Error, "خطایی رخ داد. لطفا دوباره تلاش کنید.");
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// PRIMARY PATH: API-based extraction.
    /// Login → Case list → Engineers → Fees.
    /// Returns list of Cases with specs, engineers, fees populated.
    /// Reports NOT included (no API endpoint found).
    /// </summary>
    private async Task<ExtractionResult> ExtractViaApiAsync(string username, string password, NezamDatabase db)
    {
        var cases = new List<Case>();

        try
        {
            // Login
            StatusMessage = "ورود به سیستم...";
            SetFriendly(UpdateStage.Connecting, "در حال اتصال به سرور...");
            using var api = new NezamApiClient();
            var loginOk = await api.LoginAsync(username, password);
            if (!loginOk)
            {
                AppendLog("خطا: ورود API ناموفق");
                SetFriendly(UpdateStage.Error, "ورود ناموفق بود. اطلاعات حساب را بررسی کنید.");
                return new ExtractionResult { LoginFailed = true };
            }
            AppendLog($"ورود API موفق ✓ (userId={api.UserId})");

            // The user's name becomes known here — save it for the welcome
            // message and for matching the user's own server reports.
            if (!string.IsNullOrWhiteSpace(api.UserFullName))
            {
                db.SaveSetting(SettingKeys.ApiUserName, api.UserFullName);
                UserDisplayName = api.UserFullName;
            }

            // Get all cases
            StatusMessage = "دریافت لیست پرونده‌ها...";
            SetFriendly(UpdateStage.Receiving, "در حال دریافت اطلاعات پروژه‌ها...");
            var apiCases = await api.GetCasesRawAsync();
            AppendLog($"{apiCases.Count} پرونده دریافت شد");

            // Apply start/max filters
            var startIdx = Math.Max(0, StartFromIndex - 1);
            var endIdx = MaxCases > 0 ? Math.Min(startIdx + MaxCases, apiCases.Count) : apiCases.Count;
            var casesToProcess = apiCases.Skip(startIdx).Take(endIdx - startIdx).ToList();

            int processed = 0;
            int total = casesToProcess.Count;

            foreach (var apiCase in casesToProcess)
            {
                _cts?.Token.ThrowIfCancellationRequested();

                processed++;
                Progress = (double)processed / total * 100;
                StatusMessage = $"استخراج {processed}/{total}";
                SetFriendly(UpdateStage.Receiving, "در حال دریافت اطلاعات پروژه‌ها...",
                    processed, total);

                var serial = NezamApiClient.S(apiCase, "das_serial");
                var caseNum = $"{NezamApiClient.S(apiCase, "das_year")}/{NezamApiClient.S(apiCase, "das_number")}";
                var owner = $"{NezamApiClient.S(apiCase, "own_name")} {NezamApiClient.S(apiCase, "own_famil")}".Trim();

                AppendLog($"[{processed}/{total}] {serial} | {caseNum} | {owner}");

                try
                {
                    // Map case from API
                    var caseModel = ApiExtractor.MapCase(apiCase);

                    // Filter incomplete if requested
                    if (FilterIncompleteOnly && db != null)
                    {
                        var cn = caseModel.CaseNumber;
                        var specOk = !ExtractSpecifications || db.LoadSpecificationForCase(cn) != null;
                        var engOk = !ExtractEngineers || (db.LoadEngineerCountForCase(cn)) > 0;
                        var feesOk = !ExtractFees || (db.LoadFeeCountForCase(cn)) > 0;
                        if (specOk && engOk && feesOk)
                        {
                            AppendLog($"  SKIP: {owner} (داده‌ها موجود است)");
                            continue;
                        }
                    }

                    // Get engineers
                    if (ExtractEngineers)
                    {
                        var dbId = 0;
                        if (apiCase.TryGetValue("db_id", out var dbVal) && dbVal is int dbI) dbId = dbI;
                        var apiEngineers = await api.GetEngineersRawAsync(dbId);
                        caseModel.Engineers = ApiExtractor.MapEngineers(apiEngineers);
                    }

                    // Get fees
                    if (ExtractFees)
                    {
                        var dbId = 0;
                        if (apiCase.TryGetValue("db_id", out var dbVal) && dbVal is int dbI) dbId = dbI;
                        var apiFees = await api.GetFeesRawAsync(dbId);
                        caseModel.Fees = ApiExtractor.MapFees(apiFees);
                    }

                    // Get reports
                    if (ExtractReports)
                    {
                        var dbId = 0;
                        if (apiCase.TryGetValue("db_id", out var dbVal2) && dbVal2 is int dbI2) dbId = dbI2;
                        var apiReports = await api.GetReportsRawAsync(dbId);
                        caseModel.Reports = ApiExtractor.MapReports(apiReports);
                    }

                    // Skip if specs already complete
                    if (!UpdateAllSpecifications && caseModel.Specification != null)
                    {
                        var s = caseModel.Specification;
                        bool specComplete = !string.IsNullOrEmpty(s.BuildingGroup)
                            && !string.IsNullOrEmpty(s.PermitNumber)
                            && !string.IsNullOrEmpty(s.StructureType);
                        if (specComplete) caseModel.Specification = null;
                    }

                    cases.Add(caseModel);
                    AppendLog($"  OK: {owner} — Eng:{caseModel.Engineers.Count} Fees:{caseModel.Fees.Count}");
                }
                catch (Exception ex)
                {
                    AppendLog($"  FAIL: {owner} - {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"خطای API: {ex.Message}");
        }

        return new ExtractionResult { Cases = cases };
    }

    private void StopExtraction()
    {
        _cts?.Cancel();
        StatusMessage = "در حال توقف...";
    }

    /// <summary>
    /// Download report files from API and save to outputs folder.
    /// Structure: outputs/{row}_{owner}_{caseNumber}/{reportNum}_{reportType}.{ext}
    /// </summary>
    private async Task DownloadReportFilesAsync(List<Case> cases, string username, string password)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };

            // Login for file download
            var payload = System.Text.Json.JsonSerializer.Serialize(new { ozv_num = username, ozv_pass = password, ozv_type = 0 });
            var loginReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://service.yazdnezam.ir:8033/panel/api/login")
            { Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
            var loginResp = await http.SendAsync(loginReq);
            var token = System.Text.Json.JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString()!;
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(token);

            var userResp = await http.GetAsync("http://service.yazdnezam.ir:8033/panel/api/user");
            var userJson = System.Text.Json.JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());
            var userId = userJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();
            var cityId = userJson.RootElement.GetProperty("user").GetProperty("user_shahrestan").GetInt32();

            // Get raw cases for db_id mapping
            var caseResp = await http.GetAsync($"http://service.yazdnezam.ir:8033/panel/api/showParvandeNezaratMeybod/{userId}/{cityId}");
            var rawCases = System.Text.Json.JsonDocument.Parse(await caseResp.Content.ReadAsStringAsync());

            // Build dbId lookup
            var dbIdMap = new Dictionary<string, int>();
            foreach (var rc in rawCases.RootElement.EnumerateArray())
            {
                var serial = NezamApiClient.S(rc, "das_serial");
                if (rc.TryGetProperty("db_id", out var dbVal) && dbVal.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dbIdMap[serial] = dbVal.GetInt32();
            }

            var outputBase = string.IsNullOrEmpty(OutputPath) 
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "outputs") 
                : OutputPath;
            Directory.CreateDirectory(outputBase);

            int totalFiles = 0;
            int skippedFiles = 0;
            int caseNum = 0;

            foreach (var c in cases)
            {
                caseNum++;
                if (!dbIdMap.TryGetValue(c.Serial, out var dbId)) continue;

                // Get reports for this case
                var rptPayload = System.Text.Json.JsonSerializer.Serialize(new { db_id = dbId.ToString(), sha_id = cityId.ToString() });
                var rptReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://service.yazdnezam.ir:8033/panel/api/getGozareshat")
                { Content = new System.Net.Http.StringContent(rptPayload, System.Text.Encoding.UTF8, "application/json") };
                var rptResp = await http.SendAsync(rptReq);
                var rptJson = System.Text.Json.JsonDocument.Parse(await rptResp.Content.ReadAsStringAsync());

                if (rptJson.RootElement.GetArrayLength() == 0) continue;

                // Create case folder
                var safeOwner = (c.Owner ?? "").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                var safeCaseNum = (c.CaseNumber ?? "").Replace("/", "_");
                var caseFolder = Path.Combine(outputBase, $"{caseNum}_{safeOwner}_{safeCaseNum}");
                Directory.CreateDirectory(caseFolder);

                int reportNum = 0;
                foreach (var r in rptJson.RootElement.EnumerateArray())
                {
                    reportNum++;
                    var imageName = NezamApiClient.S(r, "image_name");
                    if (string.IsNullOrEmpty(imageName)) continue;

                    var reportType = NezamApiClient.S(r, "brt_report_title");
                    var safeReportType = reportType.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                    var ext = Path.GetExtension(imageName).ToLower();
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";

                    var downloadUrl = $"http://service.yazdnezam.ir:8033/panel/Panel/public/img/gozaresh/{imageName}";
                    var fileName = $"{reportNum}_{safeReportType}{ext}";
                    var filePath = Path.Combine(caseFolder, fileName);

                    try
                    {
                        // Check if file already exists with same size (HEAD request)
                        if (SkipExistingReportFiles && File.Exists(filePath))
                        {
                            try
                            {
                                var headReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, downloadUrl);
                                var headResp = await http.SendAsync(headReq);
                                if (headResp.IsSuccessStatusCode && headResp.Content.Headers.ContentLength.HasValue)
                                {
                                    var remoteSize = headResp.Content.Headers.ContentLength.Value;
                                    var localSize = new FileInfo(filePath).Length;
                                    if (remoteSize == localSize)
                                    {
                                        skippedFiles++;
                                        // V2: file already on disk — keep the server-side image
                                        // name on the in-memory report row (the DB save runs
                                        // after this download pass).
                                        if (reportNum >= 1 && reportNum <= c.Reports.Count && string.IsNullOrEmpty(c.Reports[reportNum - 1].ImageName))
                                            c.Reports[reportNum - 1] = c.Reports[reportNum - 1] with { ImageName = imageName };
                                        continue;
                                    }
                                }
                            }
                            catch { /* HEAD failed — download normally */ }
                        }

                        var resp = await http.GetAsync(downloadUrl);
                        if (resp.IsSuccessStatusCode)
                        {
                            var bytes = await resp.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(filePath, bytes);
                            totalFiles++;
                            // V2: keep the server-side image name on the in-memory
                            // report row (the DB save runs after this download pass).
                            if (reportNum >= 1 && reportNum <= c.Reports.Count && string.IsNullOrEmpty(c.Reports[reportNum - 1].ImageName))
                                c.Reports[reportNum - 1] = c.Reports[reportNum - 1] with { ImageName = imageName };
                        }
                    }
                    catch { }
                }

                if (caseNum % 10 == 0 || caseNum == cases.Count)
                {
                    AppendLog($"  فایل‌ها: {caseNum}/{cases.Count} (دانلود: {totalFiles} | رد شده: {skippedFiles})");
                    SetFriendly(UpdateStage.Downloading, "در حال دریافت فایل‌های پیوست...",
                        caseNum, cases.Count,
                        $"{totalFiles} فایل دریافت شد");
                }
            }

            AppendLog($"دانلود فایل‌ها: {totalFiles} فایل دانلود شد + {skippedFiles} فایل رد شده (حجم یکسان) از {cases.Count} پرونده");
        }
        catch (Exception ex)
        {
            AppendLog($"خطای دانلود فایل‌ها: {ex.Message}");
        }
    }

    private void LoadLatest()
    {
        try
        {
            var db = DatabaseService.Instance;
            var snapshotId = db.GetLastValidSnapshotId();
            if (snapshotId == 0)
            {
                AppendLog("هنوز داده‌ای ذخیره نشده");
                StatusMessage = "داده‌ای موجود نیست";
                SetFriendly(UpdateStage.Idle, "هنوز داده‌ای ذخیره نشده است.");
                return;
            }
            var cases = db.LoadCases(snapshotId);
            AppendLog($"آخرین اسنپ‌شات: {cases.Count} پرونده بارگذاری شد");
            StatusMessage = $"{cases.Count} پرونده بارگذاری شد";
            SetFriendly(UpdateStage.Success, "آخرین اطلاعات بارگذاری شد.",
                cases.Count, cases.Count, $"{cases.Count} پرونده");
        }
        catch (Exception ex)
        {
            AppendLog($"خطا: {ex.Message}");
            StatusMessage = "خطا";
            SetFriendly(UpdateStage.Error, "بارگذاری ناموفق بود.");
        }
    }
}
