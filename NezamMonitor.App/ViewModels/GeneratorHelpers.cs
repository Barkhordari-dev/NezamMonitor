using System.Windows;
using NezamMonitor.App.Services;
using NezamMonitor.Core.Data;

namespace NezamMonitor.App.ViewModels;

public static class GeneratorHelpers
{
    /// <summary>
    /// بررسی تکراری بودن گزارش قبل از تولید
    /// </summary>
    public static bool IsReportGenerateDuplicate(string caseNumber, int stage, DateTime reportDate, string numberFormat)
    {
        var db = DatabaseService.Instance;
        var reports = db.GetGeneratedReports();
        var now = reportDate.ToString("yyyy/MM/dd");

        return reports.Exists(r =>
            r.CaseNumber == caseNumber &&
            r.Stage == stage &&
            r.CreatedAt == now &&
            r.NumberFormat == numberFormat);
    }
}
