namespace NezamMonitorPro.Core.Data;

/// <summary>
/// DUP-002: همه کلیدهای جدول Settings در یک جا.
/// مقدار رشته‌ها دقیقا همان‌هایی است که قبلا در کد پراکنده بود؛
/// فقط به‌جای لیترال رشته‌ای، از این ثابت‌ها استفاده می‌شود تا غلط تایپی
/// در نام کلید در کامپایل معلوم شود، نه در سکوت زمان اجرا.
/// </summary>
public static class SettingKeys
{
    // ── ورود به سامانه ──
    public const string Username = "username";
    public const string Password = "password";
    public const string RememberMe = "remember_me";
    public const string ApiToken = "api_token";
    public const string ApiUserName = "api_user_name";

    // ── تم ──
    public const string Theme = "theme";
    public const string ThemePrefix = "theme_";
    public const string ThemeName = "theme_Name";
    public const string ThemeColors = "theme_Colors";
    public const string ThemeFonts = "theme_Fonts";
    public const string ThemeDimensions = "theme_Dimensions";
    public const string ThemeButtonLayout = "theme_ButtonLayout";
    public const string ThemeLayoutDirection = "theme_LayoutDirection";
    public const string ThemeTableAlignment = "theme_TableAlignment";
    public const string ThemeButtonStyle = "theme_ButtonStyle";

    // ── تولید گزارش ──
    public const string SignedReport = "signed_report";
    public const string SignedTemplatesPath = "signed_templates_path";
    public const string TemplatePaths = "template_paths";
    public const string OutputPath = "output_path";
    public const string GeneratePdf = "generate_pdf";
    public const string ScheduleExpanded = "schedule_expanded";
    public const string ProductionExpanded = "production_expanded";
    public const string ReportIntervalGroupA = "report_interval_group_a";
    public const string ReportIntervalGroupB = "report_interval_group_b";

    // ── پرونده‌ها ──
    public const string CasesSplitterRatio = "cases_splitter_ratio";
    public const string CasesColumnOrder = "cases_column_order";

    // ── عمومی ──
    public const string FirstRunDone = "first_run_done";
    public const string UiZoom = "ui_zoom";

    // ── منوی مخفی ──
    public const string HiddenMenuFolderId = "hidden_menu_folder_id";
    public const string HiddenMenuCachedName = "hidden_menu_cached_name";
    public const string HiddenMenuCachedAt = "hidden_menu_cached_at";
    public const string UiCustom = "ui_custom";

    // ── پیشوندهای کلیدهای داینامیک ──
    public const string ColumnAlignPrefix = "col_align_";
    public const string ColumnAlignHeaderCenterPrefix = "col_align_hdr_center_";

    /// <summary>کلید ماه‌های مرحله: report_group_&lt;group&gt;_stage_&lt;stage&gt;_months (گروه a/b/c/d، مرحله ۱ تا ۴).</summary>
    public static string ReportGroupStageMonths(string group, int stage)
        => $"report_group_{group}_stage_{stage}_months";

    /// <summary>کلید تراز ستون جدول: col_align_&lt;view&gt;_&lt;grid&gt;_&lt;index&gt;_&lt;header&gt;.</summary>
    public static string ColumnAlign(string view, string grid, int index, string headerKey)
        => $"{ColumnAlignPrefix}{view}_{grid}_{index}_{headerKey}";

    /// <summary>کلید وسط‌چین بودن هدر ستون جدول.</summary>
    public static string ColumnAlignHeaderCenter(string view, string grid, int index, string headerKey)
        => $"{ColumnAlignHeaderCenterPrefix}{view}_{grid}_{index}_{headerKey}";
}
