using NezamMonitor.Core;
using NezamMonitor.Core.Diff;
using NezamMonitor.Core.Models;
using Xunit;

namespace NezamMonitor.Tests;

public class DiffTests
{
    private static Case MakeCase(string caseNo, string serial, string owner, string? permit = null)
    {
        var c = new Case { CaseNumber = caseNo, Serial = serial, Owner = owner };
        if (permit is not null)
            c.Specification = new CaseSpecification { PermitNumber = permit };
        return c;
    }

    [Fact]
    public void Compare_IdenticalSnapshots_AllUnchanged()
    {
        var snap = new List<Case> { MakeCase("1404/514", "4565", "سمیرا رجبی", "1404/0805") };
        var result = SnapshotComparer.Compare(snap, snap.Select(c => Clone(c)).ToList());
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Modified);
        Assert.Single(result.Unchanged);
    }

    [Fact]
    public void Compare_NewCase_IsAdded()
    {
        var prev = new List<Case> { MakeCase("1404/514", "4565", "سمیرا") };
        var curr = new List<Case>
        {
            MakeCase("1404/514", "4565", "سمیرا"),
            MakeCase("1404/630", "4623", "معصومه")
        };
        var result = SnapshotComparer.Compare(prev, curr);
        Assert.Single(result.Added);
        Assert.Equal("1404/630", result.Added[0].CaseNumber);
    }

    [Fact]
    public void Compare_MissingCase_IsRemoved()
    {
        var prev = new List<Case> { MakeCase("1404/514", "4565", "سمیرا"), MakeCase("1404/630", "4623", "معصومه") };
        var curr = new List<Case> { MakeCase("1404/514", "4565", "سمیرا") };
        var result = SnapshotComparer.Compare(prev, curr);
        Assert.Single(result.Removed);
        Assert.Equal("1404/630", result.Removed[0].CaseNumber);
    }

    [Fact]
    public void Compare_FieldChange_IsDetectedWithOldNewValues()
    {
        var prev = new List<Case> { MakeCase("1404/514", "4565", "سمیرا", "1404/0805") };
        var curr = new List<Case> { MakeCase("1404/514", "4565", "سمیرا", "1404/0806") };
        var result = SnapshotComparer.Compare(prev, curr);
        Assert.Single(result.Modified);
        var change = Assert.Single(result.FieldChanges);
        Assert.Equal("PermitNumber", change.Field);
        Assert.Equal("1404/0805", change.OldValue);
        Assert.Equal("1404/0806", change.NewValue);
    }

    [Fact]
    public void Compare_EngineerReordering_IsNotAChange()
    {
        var c1 = MakeCase("1404/514", "4565", "سمیرا");
        c1.Engineers.Add(new Engineer("معماری", "قپانی"));
        c1.Engineers.Add(new Engineer("عمران", "قپانی"));

        var c2 = MakeCase("1404/514", "4565", "سمیرا");
        c2.Engineers.Add(new Engineer("عمران", "قپانی"));   // different order
        c2.Engineers.Add(new Engineer("معماری", "قپانی"));

        var result = SnapshotComparer.Compare(new[] { c1 }, new[] { c2 });
        Assert.Single(result.Unchanged);
        Assert.Empty(result.FieldChanges);
    }

    [Fact]
    public void Compare_NewEngineer_IsDetected()
    {
        var c1 = MakeCase("1404/514", "4565", "سمیرا");
        c1.Engineers.Add(new Engineer("معماری", "قپانی"));

        var c2 = MakeCase("1404/514", "4565", "سمیرا");
        c2.Engineers.Add(new Engineer("معماری", "قپانی"));
        c2.Engineers.Add(new Engineer("مکانیک", "برخورداری"));

        var result = SnapshotComparer.Compare(new[] { c1 }, new[] { c2 });
        Assert.Single(result.Modified);
        var change = Assert.Single(result.FieldChanges);
        Assert.Equal("Engineer", change.Field);
        Assert.Contains("برخورداری", change.NewValue);
    }

    [Fact]
    public void Compare_WhitespaceOnlyDifference_IsIgnored()
    {
        var c1 = MakeCase("1404/514", "4565", "سمیرا  رجبی");   // double space
        var c2 = MakeCase("1404/514", "4565", "سمیرا رجبی");    // single space
        var result = SnapshotComparer.Compare(new[] { c1 }, new[] { c2 });
        Assert.Single(result.Unchanged);
    }

    [Fact]
    public void Compare_PersianVsLatinDigits_AreEquivalent()
    {
        var c1 = MakeCase("1404/514", "4565", "سمیرا");
        c1.ReportDate1 = "۱۴۰۴/۱۲/۰۴";  // Persian digits
        var c2 = MakeCase("1404/514", "4565", "سمیرا");
        c2.ReportDate1 = "1404/12/04";  // Latin digits
        var result = SnapshotComparer.Compare(new[] { c1 }, new[] { c2 });
        Assert.Single(result.Unchanged);
    }

    [Fact]
    public void RowIndex_IsNotIdentity()
    {
        // Same case at different visual positions must compare equal
        var a = MakeCase("1404/514", "4565", "سمیرا");
        var b = MakeCase("1404/514", "4565", "سمیرا");
        Assert.Equal(a.Key.ToString(), b.Key.ToString());
        var result = SnapshotComparer.Compare(new[] { a }, new[] { b });
        Assert.Single(result.Unchanged);
    }

    private static Case Clone(Case c)
    {
        var copy = new Case
        {
            CaseNumber = c.CaseNumber, Serial = c.Serial, Owner = c.Owner,
            OwnerMobile = c.OwnerMobile, Responsibility = c.Responsibility,
            CapacityDate = c.CapacityDate, Office = c.Office,
            ReportDate1 = c.ReportDate1, ReportDate2 = c.ReportDate2, ReportDate3 = c.ReportDate3,
        };
        if (c.Specification is not null)
        {
            copy.Specification = new CaseSpecification();
            foreach (var prop in typeof(CaseSpecification).GetProperties())
                if (prop.CanWrite) prop.SetValue(copy.Specification, prop.GetValue(c.Specification));
        }
        copy.Engineers = c.Engineers.ToList();
        copy.Fees = c.Fees.ToList();
        copy.Reports = c.Reports.ToList();
        return copy;
    }
}
