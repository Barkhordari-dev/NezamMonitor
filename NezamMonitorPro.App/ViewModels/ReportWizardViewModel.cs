using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using NezamMonitorPro.Core;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;
using NezamMonitorPro.Core.Reports;
using NezamMonitorPro.App.Services;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>One editable template field (wizard step 1). Value starts as the
/// normal generation's auto value; IsEdited turns on the moment the text
/// differs from it — only edited fields are sent to the generator as
/// overrides, everything else keeps the normal auto value.</summary>
public sealed class WizardFieldRow : ViewModelBase
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";

    private string _autoValue = "";
    public string AutoValue
    {
        get => _autoValue;
        set { if (SetProperty(ref _autoValue, value)) OnPropertyChanged(nameof(IsEdited)); }
    }

    private string _value = "";
    public string Value
    {
        get => _value;
        set { if (SetProperty(ref _value, value)) OnPropertyChanged(nameof(IsEdited)); }
    }

    public bool IsEdited => !string.Equals(_value, _autoValue, StringComparison.Ordinal);
}

/// <summary>One status option of a checkbox row (wizard step 2). Checking
/// one unchecks the others in the same row — radio behaviour, exactly one
/// per row.</summary>
public sealed class WizardCheckOption : ViewModelBase
{
    private readonly WizardCheckRow _row;

    public WizardCheckOption(WizardCheckRow row, string label)
    {
        _row = row;
        Label = label;
    }

    public string Label { get; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (!SetProperty(ref _isChecked, value)) return;
            if (value) _row.OnOptionChecked(this);
        }
    }
}

/// <summary>One row of the template's checkbox table (wizard step 2).</summary>
public sealed class WizardCheckRow : ViewModelBase
{
    public WizardCheckRow(int index, string number, string description, IReadOnlyList<string> headers)
    {
        Index = index;
        Number = number;
        Description = description;
        Options = new ObservableCollection<WizardCheckOption>(
            headers.Select(h => new WizardCheckOption(this, h)));
    }

    /// <summary>0-based position among the parsed data rows — the key the
    /// generator's marking uses.</summary>
    public int Index { get; }
    public string Number { get; }
    public string Description { get; }
    public ObservableCollection<WizardCheckOption> Options { get; }

    /// <summary>0-based index among the file's own status columns, or -1
    /// when nothing is picked in this row.</summary>
    public int SelectedColumn => Options.ToList().FindIndex(o => o.IsChecked);

    public bool HasSelection => SelectedColumn >= 0;

    internal void OnOptionChecked(WizardCheckOption checkedOption)
    {
        foreach (var o in Options)
            if (!ReferenceEquals(o, checkedOption) && o.IsChecked)
                o.IsChecked = false;
        OnPropertyChanged(nameof(SelectedColumn));
        OnPropertyChanged(nameof(HasSelection));
    }
}

/// <summary>One numbered توضیحات item (wizard step 3).</summary>
public sealed class WizardDescRow : ViewModelBase
{
    private string _text = "";
    public string Text { get => _text; set => SetProperty(ref _text, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

/// <summary>One stage in the picker (wizard step 0). The display name
/// already carries the «پیشنهاد» / «قبلا تولید شده» hint.</summary>
public sealed class WizardStageOption
{
    public int Number { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsSuggested { get; init; }
    public bool IsRegistered { get; init; }
}

/// <summary>The step-by-step report wizard («صدور مرحله‌ای گزارش»).
/// It only collects and edits inputs — the final generation goes through
/// the SAME StageReportGenerator.GenerateReport / history / PDF path the
/// normal Generate button uses, so the two can never drift apart.
/// Constructed directly by the window (not through DI).</summary>
public sealed class ReportWizardViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private readonly Case _case;
    private readonly int _rowNumber;
    private readonly StageReportGenerator _generator;
    private readonly string _outputPath;
    private readonly string _templatePath;

    public ReportWizardViewModel(NezamDatabase db, Case c, int rowNumber)
    {
        _db = db;
        _case = c;
        _rowNumber = rowNumber;

        var exeDir = DatabaseService.GetAppDirectory();
        _templatePath = Path.Combine(exeDir, "templates");
        _outputPath = Path.Combine(exeDir, "outputs");
        _generator = CreateGenerator();
        _makePdf = _db.GetSetting(SettingKeys.GeneratePdf) == "1";

        // The discipline hint is the same source GenerateReport uses: the
        // first engineer with a discipline, else the first fee's.
        DisciplineHint = _case.Engineers?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Discipline))?.Discipline
            ?? _case.Fees?.FirstOrDefault()?.Discipline;

        StageCount = ReportWizardLogic.StageCountForCase(_case.Specification?.BuildingGroup ?? "", DisciplineHint);
        var registered = RegisteredStages();
        SuggestedStage = ReportWizardLogic.SuggestedStage(StageCount, registered);
        StageOptions = new ObservableCollection<WizardStageOption>(
            Enumerable.Range(1, StageCount).Select(n => new WizardStageOption
            {
                Number = n,
                DisplayName = $"مرحله {StageReportRules.StageName(n)}"
                    + (n == SuggestedStage ? " — پیشنهاد" : "")
                    + (registered.Contains(n) ? " (قبلا تولید شده)" : ""),
                IsSuggested = n == SuggestedStage,
                IsRegistered = registered.Contains(n),
            }));

        SelectedStage = SuggestedStage > 0 ? SuggestedStage : 1;
    }

    // ---------- The case and the stage ----------

    public string CaseTitle => $"پرونده {_case.CaseNumber} — {_case.Owner}";

    public string? DisciplineHint { get; }

    public int StageCount { get; }

    public ObservableCollection<WizardStageOption> StageOptions { get; }

    public int SuggestedStage { get; }

    private int _selectedStage;
    public int SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (!SetProperty(ref _selectedStage, value)) return;
            LoadStage(value);
        }
    }

    public string StageDisplayName => $"مرحله {StageReportRules.StageName(SelectedStage)}";

    // ---------- Steps ----------

    private int _currentStep;
    /// <summary>0 = stage picker, 1 = fields, 2 = checkbox table,
    /// 3 = توضیحات rows, 4 = full توضیحات text, 5 = finish.</summary>
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (!SetProperty(ref _currentStep, value)) return;
            OnPropertyChanged(nameof(StepTitle));
            OnPropertyChanged(nameof(StepCounter));
            OnPropertyChanged(nameof(IsStep0));
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(IsStep4));
            OnPropertyChanged(nameof(IsStep5));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(ShowNext));
            OnPropertyChanged(nameof(IsLastStep));
            OnPropertyChanged(nameof(CanGenerate));
        }
    }

    public string StepTitle => CurrentStep switch
    {
        0 => "انتخاب مرحله",
        1 => "فیلدهای تمپلیت",
        2 => "جدول چک‌باکس",
        3 => "توضیحات",
        4 => "متن کامل توضیحات",
        5 => "پایان و تولید",
        _ => ""
    };

    public string StepCounter => CurrentStep == 0
        ? "اول مرحله را انتخاب کن"
        : $"قدم {CurrentStep} از ۵";

    public bool IsStep0 => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;
    public bool IsLastStep => CurrentStep == 5;

    public bool CanGoBack => CurrentStep > 0 && !IsBusy;

    public bool CanGoNext => CurrentStep < 5 && !IsBusy
        && (CurrentStep != 0 || string.IsNullOrEmpty(TemplateError));

    public bool ShowNext => CurrentStep < 5;

    public bool CanGenerate => CurrentStep == 5 && !IsBusy && string.IsNullOrEmpty(TemplateError);

    public void GoNext()
    {
        if (!CanGoNext) return;
        var next = CurrentStep + 1;
        // Skip the stages the template does not have.
        if (next == 2 && !HasCheckboxTable) next = 3;
        if (next == 3 && !HasDescriptionTable) next = 5;
        if (next == 4 && !HasDescriptionTable) next = 5;
        if (next > 5) next = 5;
        if (next == 4) ComposeFullText();
        if (next == 5) BuildSummary();
        CurrentStep = next;
    }

    public void GoBack()
    {
        if (!CanGoBack) return;
        var prev = CurrentStep - 1;
        if (prev == 4 && !HasDescriptionTable) prev = 3;
        if (prev == 3 && !HasDescriptionTable) prev = 2;
        if (prev == 2 && !HasCheckboxTable) prev = 1;
        if (prev < 0) prev = 0;
        // Leaving the full-text step backwards discards its edit: the
        // rows are the source again, and step 4 is rebuilt from them.
        if (CurrentStep == 4 && prev == 3) _fullTextEdited = false;
        CurrentStep = prev;
    }

    // ---------- Options shared by steps 1 and 5 ----------

    private bool _useLtr;
    public bool UseLtr
    {
        get => _useLtr;
        set
        {
            if (!SetProperty(ref _useLtr, value)) return;
            RefreshAutoValues();
        }
    }

    private bool _makePdf;
    public bool MakePdf { get => _makePdf; set => SetProperty(ref _makePdf, value); }

    // ---------- Step 1: fields ----------

    public ObservableCollection<WizardFieldRow> FieldRows { get; } = new();

    // ---------- Step 2: checkbox table ----------

    public ObservableCollection<WizardCheckRow> CheckRows { get; } = new();

    private bool _hasCheckboxTable;
    public bool HasCheckboxTable
    {
        get => _hasCheckboxTable;
        set { if (SetProperty(ref _hasCheckboxTable, value)) OnPropertyChanged(nameof(NoCheckboxTable)); }
    }

    public bool NoCheckboxTable => !HasCheckboxTable;

    private string _checkboxMissingMessage = "";
    public string CheckboxMissingMessage
    {
        get => _checkboxMissingMessage;
        private set => SetProperty(ref _checkboxMissingMessage, value);
    }

    // ---------- Step 1: fields ----------

    private string _noFieldsMessage = "";
    /// <summary>Shown when the stage's template has no editable
    /// placeholder at all (step 1 is then just the LTR switch).</summary>
    public string NoFieldsMessage
    {
        get => _noFieldsMessage;
        private set => SetProperty(ref _noFieldsMessage, value);
    }

    // ---------- Step 3: توضیحات ----------

    public ObservableCollection<WizardDescRow> DescRows { get; } = new();

    // ---------- Step 4: full توضیحات text ----------

    /// <summary>The template's توضیحات lines around the numbered items
    /// (title + intro before, closing after) — step 4 rebuilds the whole
    /// text from these + the current rows.</summary>
    private IReadOnlyList<string> _descriptionPrefixLines = Array.Empty<string>();
    private IReadOnlyList<string> _descriptionSuffixLines = Array.Empty<string>();

    /// <summary>True once the user typed in the step-4 box. While false,
    /// entering step 4 rebuilds the text from the rows; once true, the
    /// box keeps the user's text. Going back to step 3 clears it.</summary>
    private bool _fullTextEdited;

    private string _descriptionFullText = "";
    /// <summary>The whole توضیحات text as it will appear in the report
    /// (title + intro + numbered rows + closing), editable in step 4.</summary>
    public string DescriptionFullText
    {
        get => _descriptionFullText;
        set
        {
            if (!SetProperty(ref _descriptionFullText, value)) return;
            _fullTextEdited = true;
        }
    }

    /// <summary>Build the step-4 text from the template's prefix/suffix
    /// lines and the current step-3 rows (unless the user already edited
    /// the box — then it is left alone).</summary>
    private void ComposeFullText()
    {
        if (_fullTextEdited) return;
        _descriptionFullText = ReportWizardLogic.BuildDescriptionFullText(
            _descriptionPrefixLines,
            DescRows.Select(r => r.Text).ToList(),
            _descriptionSuffixLines);
        _fullTextEdited = false;
        OnPropertyChanged(nameof(DescriptionFullText));
    }

    private bool _hasDescriptionTable;
    public bool HasDescriptionTable
    {
        get => _hasDescriptionTable;
        set { if (SetProperty(ref _hasDescriptionTable, value)) OnPropertyChanged(nameof(NoDescriptionTable)); }
    }

    public bool NoDescriptionTable => !HasDescriptionTable;

    private string _descriptionMissingMessage = "";
    public string DescriptionMissingMessage
    {
        get => _descriptionMissingMessage;
        private set => SetProperty(ref _descriptionMissingMessage, value);
    }

    // ---------- Template / status ----------

    private string _templateError = "";
    public string TemplateError
    {
        get => _templateError;
        set
        {
            if (!SetProperty(ref _templateError, value)) return;
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGenerate));
        }
    }

    public string TemplateFileName { get; private set; } = "";

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGenerate));
        }
    }

    private bool _isGenerated;
    public bool IsGenerated { get => _isGenerated; set => SetProperty(ref _isGenerated, value); }

    public string GeneratedPath { get; private set; } = "";
    public string GeneratedPdfPath { get; private set; } = "";

    private string _resultMessage = "";
    public string ResultMessage { get => _resultMessage; set => SetProperty(ref _resultMessage, value); }

    private string _summaryText = "";
    public string SummaryText { get => _summaryText; set => SetProperty(ref _summaryText, value); }

    // ---------- Generator factory (mirrors GeneratorViewModel.CreateGenerator) ----------

    private StageReportGenerator CreateGenerator()
    {
        var generator = new StageReportGenerator(_templatePath);
        generator.UserName = _db.GetSetting(SettingKeys.ApiUserName) ?? "";
        generator.UseSignedTemplates = _db.GetSetting(SettingKeys.SignedReport) == "1";
        var signedPath = _db.GetSetting(SettingKeys.SignedTemplatesPath);
        generator.SetSignedTemplateDirectory(
            string.IsNullOrWhiteSpace(signedPath) ? Path.Combine(_templatePath, "signed") : signedPath);

        foreach (var dir in GetExtraTemplateDirectories())
            generator.AddTemplateDirectory(dir);

        return generator;
    }

    private IEnumerable<string> GetExtraTemplateDirectories()
    {
        var dirs = new List<string>();

        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                dirs.Add(Path.Combine(profile, "Downloads", "NezamMonitor-Windows", "templates"));
                dirs.Add(Path.Combine(profile, "Downloads", "NezamMonitor-Windows", "Templates"));
                dirs.Add(Path.Combine(profile, "Downloads", "NezamMonitor-Windows"));
                dirs.Add(Path.Combine(profile, "Downloads", "templates"));
            }

            dirs.Add(@"E:\Nezam Monitor-Mahdi2\Source_and_Development\templates");
            dirs.Add(@"E:\NezamMonitor-Mahdi2\Source_and_Development\templates");
            dirs.Add(@"E:\Nezam Monitor-Mahdi2\templates");
        }
        catch { }

        try
        {
            var extra = _db.GetSetting(SettingKeys.TemplatePaths);
            if (!string.IsNullOrWhiteSpace(extra))
            {
                foreach (var part in extra.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    dirs.Add(part);
            }
        }
        catch { }

        return dirs;
    }

    // ---------- Registered stages (for the suggestion) ----------

    private HashSet<int> RegisteredStages()
    {
        var registered = new HashSet<int>();
        try
        {
            foreach (var r in _db.GetGeneratedReports())
                if (r.CaseNumber == _case.CaseNumber)
                    registered.Add(r.Stage);
        }
        catch { }

        // The user's OWN server reports: a case's other engineers' reports
        // must not mark this user's report as produced.
        var userName = FilterHelper.Normalize(_db.GetSetting(SettingKeys.ApiUserName));
        if (!string.IsNullOrEmpty(userName))
        {
            for (var stage = 1; stage <= StageCount; stage++)
            {
                foreach (var r in StageReportRules.FindMatchingReports(_case, stage))
                {
                    if (string.Equals(FilterHelper.Normalize(r.Engineer), userName, StringComparison.OrdinalIgnoreCase))
                    {
                        registered.Add(stage);
                        break;
                    }
                }
            }
        }
        return registered;
    }

    // ---------- Loading a stage's template ----------

    private void LoadStage(int stage)
    {
        FieldRows.Clear();
        CheckRows.Clear();
        DescRows.Clear();
        HasCheckboxTable = false;
        HasDescriptionTable = false;
        _descriptionPrefixLines = Array.Empty<string>();
        _descriptionSuffixLines = Array.Empty<string>();
        _descriptionFullText = "";
        _fullTextEdited = false;
        CheckboxMissingMessage = "";
        DescriptionMissingMessage = "";
        NoFieldsMessage = "";
        TemplateFileName = "";
        TemplateError = "";
        IsGenerated = false;
        ResultMessage = "";
        StatusMessage = "";

        if (stage < 1 || stage > StageCount)
        {
            TemplateError = "مرحله انتخاب نشده.";
            return;
        }

        var templatePath = _generator.FindTemplateForStage(stage, DisciplineHint);
        if (templatePath == null)
        {
            TemplateError = BuildTemplateMissingMessage(stage);
            return;
        }

        TemplateFileName = Path.GetFileName(templatePath);

        var data = ReportWizardLogic.LoadWizardTemplateData(templatePath);
        if (data == null)
        {
            TemplateError = $"تمپلیت «{TemplateFileName}» باز نشد؛ شاید فایل خراب است. یک تمپلیت سالم را در پوشه تمپلیت‌ها بگذار.";
            return;
        }

        // Step 1: ONLY the fields whose placeholder this stage's template
        // actually contains — a field the template does not have cannot
        // change the report, so showing it would only confuse. Each row is
        // pre-filled with the normal generation's auto value (shown in
        // Persian letters).
        var auto = _generator.BuildAutoFieldValues(_case, DateTime.Now, UseLtr);
        foreach (var key in data.PresentFieldKeys)
        {
            var display = ReportWizardLogic.ToPersianDisplay(
                auto.TryGetValue(key, out var v) ? v : "");
            FieldRows.Add(new WizardFieldRow
            {
                Key = key,
                Label = ReportWizardLogic.FieldLabel(key),
                AutoValue = display,
                Value = display,
            });
        }
        if (FieldRows.Count == 0)
            NoFieldsMessage = "این قالب هیچ فیلد قابل ویرایشی ندارد؛ مستقیم به قدم بعد برو.";

        // Step 2: the template's own checkbox table (never invented).
        if (data.CheckboxTable != null)
        {
            HasCheckboxTable = true;
            foreach (var row in data.CheckboxTable.Rows)
            {
                CheckRows.Add(new WizardCheckRow(
                    row.Index, row.Number,
                    ReportWizardLogic.ToPersianDisplay(row.Description),
                    data.CheckboxTable.Headers));
            }
        }
        else
        {
            CheckboxMissingMessage = "این تمپلیت جدول چک‌باکس ندارد؛ مرحله ۲ رد می‌شود.";
        }

        // Step 3: exactly the template's own numbered items. When the
        // template's slots are all empty, the wizard starts with no rows
        // and the user adds them himself (or writes the whole text in
        // step 4) — nothing is invented from a fixed list.
        if (data.DescriptionItems != null)
        {
            HasDescriptionTable = true;
            _descriptionPrefixLines = data.DescriptionPrefixLines;
            _descriptionSuffixLines = data.DescriptionSuffixLines;
            _descriptionFullText = "";
            _fullTextEdited = false;
            foreach (var item in ReportWizardLogic.InitialDescriptionItems(data.DescriptionItems))
                DescRows.Add(new WizardDescRow { Text = ReportWizardLogic.ToPersianDisplay(item) });
        }
        else
        {
            DescriptionMissingMessage = "این تمپلیت جدول توضیحات ندارد؛ مرحله‌های ۳ و ۴ رد می‌شوند.";
        }

        OnPropertyChanged(nameof(StageDisplayName));
    }

    /// <summary>Recompute the auto values (e.g. after the LTR switch) and
    /// refresh every field the user has NOT edited. Edited fields keep the
    /// user's text.</summary>
    private void RefreshAutoValues()
    {
        var auto = _generator.BuildAutoFieldValues(_case, DateTime.Now, UseLtr);
        foreach (var row in FieldRows)
        {
            var wasEdited = row.IsEdited;
            row.AutoValue = ReportWizardLogic.ToPersianDisplay(
                auto.TryGetValue(row.Key, out var v) ? v : "");
            if (!wasEdited) row.Value = row.AutoValue;
        }
    }

    private string BuildTemplateMissingMessage(int stage)
    {
        var present = new List<string>();
        foreach (var dir in _generator.TemplateDirectories)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.docx"))
                {
                    var name = Path.GetFileName(file);
                    if (!name.StartsWith("~$")) present.Add(name);
                }
            }
            catch { }
        }

        var msg = $"برای {StageDisplayName} تمپلیتی پیدا نشد.";
        if (present.Count > 0)
            msg += $"\n\nفایل‌هایی که در پوشه‌های تمپلیت هست:\n{string.Join("، ", present.Distinct())}";
        else
            msg += "\n\nدر پوشه‌های تمپلیت هیچ فایل Word نیست.";
        msg += "\n\nتمپلیت را از منوی «تولید گزارش» دانلود کن یا در پوشه تمپلیت‌ها بگذار.";
        return msg;
    }

    // ---------- Step 3: description rows ----------

    public void AddDescriptionRow() => DescRows.Add(new WizardDescRow { Text = "" });

    public void RemoveDescriptionRow(WizardDescRow? row)
    {
        if (row != null) DescRows.Remove(row);
    }

    // ---------- Step 3: follow-up ----------

    public void ImportFromFollowUp()
    {
        var fu = _db.GetFollowUpEdit(_case.CaseNumber);
        if (fu == null)
        {
            StatusMessage = "برای این پرونده پیگیری ثبت نشده؛ چیزی برای وارد کردن نیست.";
            return;
        }

        var values = new[] { fu.Value.Description, fu.Value.CaseNumberEdit, fu.Value.OwnerEdit, fu.Value.AddressEdit, fu.Value.OwnerMobileEdit };
        var added = 0;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var display = ReportWizardLogic.ToPersianDisplay(value);
            if (DescRows.Any(r => r.Text == display)) continue;
            DescRows.Add(new WizardDescRow { Text = display });
            added++;
        }
        StatusMessage = added > 0
            ? $"{added} مورد از پیگیری‌ها اضافه شد."
            : "همه موارد پیگیری از قبل در توضیحات هست.";
    }

    public void ShowFollowUp()
    {
        var fu = _db.GetFollowUpEdit(_case.CaseNumber);
        if (fu == null)
        {
            MessageBox.Show("برای این پرونده پیگیری ثبت نشده.", "پیگیری پرونده",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var text = $"توضیحات:\n{fu.Value.Description}\n\n"
            + $"شماره پرونده: {fu.Value.CaseNumberEdit}\n"
            + $"مالک: {fu.Value.OwnerEdit}\n"
            + $"آدرس: {fu.Value.AddressEdit}\n"
            + $"تلفن مالک: {fu.Value.OwnerMobileEdit}";
        MessageBox.Show(text, "پیگیری پرونده", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void AddSelectedToFollowUp()
    {
        var selected = DescRows.Where(r => r.IsSelected && !string.IsNullOrWhiteSpace(r.Text)).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "اول روی توضیحاتی که می‌خواهی به پیگیری اضافه شود تیک بزن.";
            return;
        }

        var fu = _db.GetFollowUpEdit(_case.CaseNumber);
        // The other four fields are preserved exactly; when there is no
        // follow-up yet, they start from the case itself.
        var description = fu?.Description ?? "";
        var caseNumberEdit = fu?.CaseNumberEdit ?? _case.CaseNumber;
        var ownerEdit = fu?.OwnerEdit ?? _case.Owner;
        var addressEdit = fu?.AddressEdit ?? _case.Specification?.Address ?? "";
        var ownerMobileEdit = fu?.OwnerMobileEdit ?? _case.OwnerMobile;

        var stamp = PersianDateHelper.ToShamsiDateTime(DateTime.Now);
        foreach (var row in selected)
        {
            if (description.Length > 0) description += "\n";
            description += $"{stamp} — {row.Text}";
            row.IsSelected = false;
        }

        _db.SaveFollowUpEdit(_case.CaseNumber, description, caseNumberEdit, ownerEdit, addressEdit, ownerMobileEdit);
        StatusMessage = $"{selected.Count} مورد به پیگیری اضافه شد.";
    }

    // ---------- Step 5: summary + generate ----------

    private void BuildSummary()
    {
        var edited = FieldRows.Count(r => r.IsEdited);
        var marks = CheckRows.Count(r => r.HasSelection);
        var already = _db.GetGeneratedReports()
            .Any(r => r.CaseNumber == _case.CaseNumber && r.Stage == SelectedStage);

        SummaryText = $"پرونده: {_case.CaseNumber} — {_case.Owner}\n"
            + $"مرحله: {StageDisplayName}\n"
            + $"تمپلیت: {TemplateFileName}\n"
            + $"فیلد ویرایش‌شده: {edited} از {FieldRows.Count}\n"
            + $"تیک انتخاب‌شده: {marks} از {CheckRows.Count}\n"
            + $"توضیحات: {DescRows.Count} مورد"
            + (_fullTextEdited ? " (متن کامل ویرایش شده)" : "")
            + "\n"
            + (already
                ? "\nاین گزارش قبلا تولید شده؛ با زدن «تولید گزارش» ازت می‌پرسم که دوباره ساخته شود یا نه."
                : "");
    }

    /// <summary>Generate through the SAME path the normal Generate button
    /// uses: duplicate confirmation, GenerateReport with the wizard's
    /// overrides, history record, optional PDF on this thread.</summary>
    public async Task GenerateAsync()
    {
        if (IsBusy || CurrentStep != 5) return;
        IsBusy = true;
        StatusMessage = "در حال تولید گزارش…";
        ResultMessage = "";
        IsGenerated = false;

        try
        {
            var existing = _db.GetGeneratedReports()
                .FirstOrDefault(r => r.CaseNumber == _case.CaseNumber && r.Stage == SelectedStage);
            if (existing.CaseNumber != null)
            {
                var confirmMsg = $"این گزارش قبلا تولید شده است.\n\nآیا می‌خواهید آن را مجددا تولید کنید?\n\nمالک: {_case.Owner}\nمرحله: {StageDisplayName}";
                var confirm = MessageBox.Show(confirmMsg, "تولید مجدد",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                {
                    StatusMessage = "لغو شد.";
                    return;
                }
            }

            var overrides = new ReportWizardOverrides
            {
                FieldValues = FieldRows.Where(r => r.IsEdited)
                    .ToDictionary(r => r.Key, r => r.Value),
                CheckboxSelections = CheckRows.Where(r => r.HasSelection)
                    .ToDictionary(r => r.Index, r => r.SelectedColumn),
                DescriptionItems = HasDescriptionTable
                    ? DescRows.Select(r => r.Text).ToList()
                    : null,
                // When the user edited the full text (step 4), it wins
                // over the numbered rows at generation time.
                DescriptionFullText = _fullTextEdited ? DescriptionFullText : null,
            };

            var genResult = await Task.Run(() =>
                _generator.GenerateReport(_case, SelectedStage, _rowNumber,
                    _outputPath, DateTime.Now, null, UseLtr, overrides));

            if (!genResult.Success || genResult.OutputPath == null || !File.Exists(genResult.OutputPath))
            {
                StatusMessage = $"خطا: {genResult.ErrorMessage}";
                return;
            }

            _db.DeleteGeneratedReport(_case.CaseNumber, SelectedStage);
            var fileHash = NezamDatabase.ComputeFileHash(genResult.OutputPath);
            _db.SaveGeneratedReport(_case.CaseNumber, SelectedStage, TemplateFileName,
                genResult.OutputPath, _case.Owner, UseLtr ? "LTR" : "Persian", fileHash);

            var pdfNote = "";
            if (MakePdf)
            {
                using var pdfConverter = new WordToPdfConverter();
                if (!pdfConverter.IsAvailable)
                {
                    pdfNote = $" (PDF نشد: {pdfConverter.Error})";
                }
                else
                {
                    var pdfPath = Path.ChangeExtension(genResult.OutputPath, ".pdf");
                    if (pdfConverter.Convert(genResult.OutputPath, pdfPath, out var pdfError))
                    {
                        GeneratedPdfPath = pdfPath;
                        _db.SetGeneratedReportPdf(_case.CaseNumber, SelectedStage, pdfPath);
                    }
                    else
                    {
                        pdfNote = $" (PDF نشد: {pdfError})";
                    }
                }
            }

            GeneratedPath = genResult.OutputPath;
            IsGenerated = true;

            var marksNote = genResult.WizardMarksApplied > 0
                ? $" — {genResult.WizardMarksApplied} تیک ثبت شد"
                : "";
            var marksWarning = overrides.CheckboxSelections.Count > 0 && genResult.WizardMarksApplied == 0
                ? " — تیک‌ها در فایل ثبت نشد!"
                : "";
            ResultMessage = $"گزارش {StageDisplayName} ساخته شد ✓{marksNote}{marksWarning}{pdfNote}";
            StatusMessage = $"مسیر: {genResult.OutputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = "تولید گزارش با خطا مواجه شد؛ جزییات در فایل لاگ ثبت شد.";
            ErrorLog.Write(ex, "ReportWizardViewModel.GenerateAsync");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
