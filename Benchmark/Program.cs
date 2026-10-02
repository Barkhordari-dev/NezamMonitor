using System.Diagnostics;
using NezamMonitor.Core.Api;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

if (args.Length < 2) { Console.WriteLine("Usage: dotnet run -- <user> <pass>"); return; }

var username = args[0];
var password = args[1];

Console.WriteLine("=== FULL API EXTRACTION → DATABASE ===");
Console.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

var totalSw = Stopwatch.StartNew();

// Login
Console.WriteLine("[1] Login...");
using var api = new NezamApiClient();
var loginOk = await api.LoginAsync(username, password);
if (!loginOk) { Console.WriteLine("FAILED"); return; }
Console.WriteLine($"    OK (userId={api.UserId}, cityId={api.CityId})");

// Get all cases
Console.WriteLine("[2] Getting case list...");
var apiCases = await api.GetCasesRawAsync();
Console.WriteLine($"    {apiCases.Count} cases");

// Get engineers, fees, reports for each
Console.WriteLine("[3] Getting engineers + fees + reports...");
var allCases = new List<Case>();
int engTotal = 0, feeTotal = 0, rptTotal = 0, errors = 0;

for (int i = 0; i < apiCases.Count; i++)
{
    var apiCase = apiCases[i];
    try
    {
        var caseModel = ApiExtractor.MapCase(apiCase);

        var dbId = 0;
        if (apiCase.TryGetValue("db_id", out var dbVal) && dbVal is int dbI) dbId = dbI;

        // Engineers
        var apiEngineers = await api.GetEngineersRawAsync(dbId);
        caseModel.Engineers = ApiExtractor.MapEngineers(apiEngineers);
        engTotal += caseModel.Engineers.Count;

        // Fees
        var apiFees = await api.GetFeesRawAsync(dbId);
        caseModel.Fees = ApiExtractor.MapFees(apiFees);
        feeTotal += caseModel.Fees.Count;

        // Reports
        var apiReports = await api.GetReportsRawAsync(dbId);
        caseModel.Reports = ApiExtractor.MapReports(apiReports);
        rptTotal += caseModel.Reports.Count;

        allCases.Add(caseModel);

        if ((i + 1) % 10 == 0 || i == apiCases.Count - 1)
            Console.WriteLine($"    {i + 1}/{apiCases.Count} done");
    }
    catch (Exception ex)
    {
        errors++;
        Console.WriteLine($"    FAIL: {NezamApiClient.S(apiCase, "das_serial")} - {ex.Message}");
    }
}

totalSw.Stop();

// Save to database
Console.WriteLine("\n[4] Saving to database...");
var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "nezam_monitor.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

using (var db = new NezamDatabase(dbPath))
{
    var snapshotId = db.CreateSnapshot(DateTime.Now);
    db.SaveSnapshot(snapshotId, allCases);
    db.FinalizeSnapshot(snapshotId, allCases.Count);
    db.SetActiveSnapshot(snapshotId);
    Console.WriteLine($"    Saved: snapshot #{snapshotId}, {allCases.Count} cases, ACTIVE");
}

// Results
Console.WriteLine();
Console.WriteLine("=== RESULTS ===");
Console.WriteLine($"Total: {totalSw.ElapsedMilliseconds}ms ({totalSw.Elapsed.TotalSeconds:F1}s)");
Console.WriteLine($"Cases: {allCases.Count}");
Console.WriteLine($"Engineers: {engTotal}");
Console.WriteLine($"Fees: {feeTotal}");
Console.WriteLine($"Reports: {rptTotal}");
Console.WriteLine($"Errors: {errors}");

// Verify
Console.WriteLine("\n=== VERIFICATION ===");
using (var db = new NezamDatabase(dbPath))
{
    var activeId = db.GetActiveSnapshotId();
    var cases = db.LoadCases(activeId);
    Console.WriteLine($"Active snapshot: #{activeId}");
    Console.WriteLine($"Cases: {cases.Count}");
    Console.WriteLine($"Engineers: {cases.Sum(c => c.Engineers.Count)}");
    Console.WriteLine($"Fees: {cases.Sum(c => c.Fees.Count)}");
    Console.WriteLine($"Reports: {cases.Sum(c => c.Reports.Count)}");
    Console.WriteLine($"Specs: {cases.Count(c => c.Specification != null)}");
    Console.WriteLine($"UsageType: {cases.Count(c => !string.IsNullOrEmpty(c.Specification?.UsageType))}");

    foreach (var c in cases.Take(3))
    {
        var spec = c.Specification;
        Console.WriteLine($"  {c.CaseNumber} | {c.Owner} | Eng:{c.Engineers.Count} Fees:{c.Fees.Count} Rpt:{c.Reports.Count} Spec:{(spec != null ? "Y" : "N")} Usage:{spec?.UsageType ?? "EMPTY"}");
    }
}

Console.WriteLine("\nDONE");
