using System.IO.Compression;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Tests;

/// <summary>Shared fixtures for the new tests (TEST-001/002).</summary>
internal static class TestHelpers
{
    /// <summary>A temp directory that deletes itself on dispose.</summary>
    internal sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"nezam_tests_{Guid.NewGuid():N}");

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            for (var i = 0; i < 5; i++)
            {
                try { Directory.Delete(Path, true); return; }
                catch (IOException) { Thread.Sleep(50); }
                catch (UnauthorizedAccessException) { Thread.Sleep(50); }
            }
        }
    }

    /// <summary>Walk up from the test binary until a repo-relative file is found.</summary>
    internal static string? FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>Repo root = the directory holding NezamMonitorPro.sln.</summary>
    internal static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "NezamMonitorPro.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>Locate the real 77-case export sample (~/workspace/nzmdata-sample).
    /// It is a local fixture, not part of the repo, so it may legitimately be
    /// absent (e.g. on a fresh Windows checkout) — callers skip gracefully.</summary>
    internal static string? FindSampleDir()
    {
        var fromEnv = Environment.GetEnvironmentVariable("NZMDATA_SAMPLE_DIR");
        if (!string.IsNullOrEmpty(fromEnv) && Directory.Exists(fromEnv)) return fromEnv;

        var root = FindRepoRoot();
        if (root != null)
        {
            var sibling = System.IO.Path.Combine(
                Directory.GetParent(root)!.FullName, "nzmdata-sample");
            if (Directory.Exists(sibling)) return sibling;
        }

        var home = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "workspace", "nzmdata-sample");
        return Directory.Exists(home) ? home : null;
    }

    /// <summary>One case with spec/engineers/fees/reports, as in DatabaseTests.</summary>
    internal static Case SampleCase(string caseNumber = "1404/514") => new()
    {
        CaseNumber = caseNumber,
        Serial = "4565",
        Owner = "سمیرا رجبی",
        OwnerMobile = "09196842392",
        Responsibility = "مکانیک",
        Specification = new CaseSpecification
        {
            PermitNumber = "1404/0805",
            PermitDate = "1404/05/01",
            RenovationCode = "123",
            Address = "شهرک طوس",
            UsageType = "مسکونی",
            BuildingGroup = "الف",
            StructureType = "بتنی",
            BlockTitle = "بلوک الف",
            BlockCount = "2",
            Floors = "3",
            Units = "6",
            Issuer = "شهرداری",
            PlanZone = "محدوده طرح",
            LandArea = "500",
            ParafArea = "400"
        },
        Engineers = { new Engineer("معماری", "مهدیه قپانی"), new Engineer("مکانیک", "علی رضایی") },
        Fees = { new Fee("مکانیک", "نظارت", "۱", "", "", "۲۳,۱۵۸,۲۹۳ ریال", "پرداخت شده", "تایید شده", "نظارت", "") },
        Reports = { new ReportRecord("۱", "معماری", "مرحله اول", "مهدیه قپانی", "معماری", "۱۴۰۴/۱۲/۰۴", "۱", "_", true) },
    };

    /// <summary>Create a snapshot with one case, finalize it and make it active.</summary>
    internal static long SeedActiveSnapshot(NezamDatabase db, string caseNumber = "1404/514")
    {
        var snapId = db.CreateSnapshot(DateTime.UtcNow);
        db.SaveSnapshot(snapId, new List<Case> { SampleCase(caseNumber) });
        db.FinalizeSnapshot(snapId, 1);
        db.SetActiveSnapshot(snapId);
        return snapId;
    }

    /// <summary>Zip manifest.json + data.json from a directory into a .nzmdata package.</summary>
    internal static void BuildPackage(string sourceDir, string packagePath)
    {
        if (File.Exists(packagePath)) File.Delete(packagePath);
        using var zip = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(System.IO.Path.Combine(sourceDir, "manifest.json"), "manifest.json");
        zip.CreateEntryFromFile(System.IO.Path.Combine(sourceDir, "data.json"), "data.json");
    }

    /// <summary>SHA-256 of a file's bytes, uppercase hex — the manifest format.</summary>
    internal static string Sha256Hex(byte[] bytes)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}
