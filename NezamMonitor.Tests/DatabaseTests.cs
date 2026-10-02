using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;
using Xunit;

namespace NezamMonitor.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"nezam_test_{Guid.NewGuid():N}.db");
    private readonly NezamDatabase _db;

    public DatabaseTests() => _db = new NezamDatabase(_dbPath);
    public void Dispose()
    {
        _db.Dispose();
        // SQLite may hold the file briefly after dispose
        for (int i = 0; i < 5; i++)
        {
            try { File.Delete(_dbPath); return; }
            catch (IOException) { Thread.Sleep(50); }
        }
    }

    [Fact]
    public void CreateSnapshot_ReturnsId()
    {
        var id = _db.CreateSnapshot(DateTime.UtcNow);
        Assert.True(id > 0);
    }

    [Fact]
    public void FinalizeSnapshot_UpdatesStatus()
    {
        var id = _db.CreateSnapshot(DateTime.UtcNow);
        _db.FinalizeSnapshot(id, 10);
        Assert.Equal(id, _db.GetLastValidSnapshotId());
    }

    [Fact]
    public void SaveAndLoadCases_RoundTrips()
    {
        var snapId = _db.CreateSnapshot(DateTime.UtcNow);
        var cases = new List<Case>
        {
            new() { CaseNumber = "1404/514", Serial = "4565", Owner = "سمیرا رجبی",
                Specification = new CaseSpecification { PermitNumber = "1404/0805", Address = "شهرک طوس" },
                Engineers = { new Engineer("معماری", "قپانی"), new Engineer("عمران", "قپانی") },
                Fees = { new Fee("مکانیک", "نظارت", "۱", "", "", "۲۳,۱۵۸,۲۹۳ ریال", "پرداخت شده", "تایید شده", "نظارت", "") },
                Reports = { new ReportRecord("۱", "معماری", "مرحله اول", "قپانی", "معماری", "۱۴۰۴/۱۲/۰۴", "۱", "_", true) },
            }
        };
        _db.SaveSnapshot(snapId, cases);
        _db.FinalizeSnapshot(snapId, 1);

        var loaded = _db.LoadCases(snapId);
        Assert.Single(loaded);
        Assert.Equal("1404/514", loaded[0].CaseNumber);
        Assert.Equal("سمیرا رجبی", loaded[0].Owner);
        Assert.NotNull(loaded[0].Specification);
        Assert.Equal("1404/0805", loaded[0].Specification!.PermitNumber);
        Assert.Equal(2, loaded[0].Engineers.Count);
        Assert.Single(loaded[0].Fees);
        Assert.Single(loaded[0].Reports);
        Assert.True(loaded[0].Reports[0].HasFile);
    }

    [Fact]
    public void Settings_RoundTrips()
    {
        _db.SaveSetting("username", "31-4-0-04883");
        var settings = _db.LoadSettings();
        Assert.Equal("31-4-0-04883", settings["username"]);
    }
}
