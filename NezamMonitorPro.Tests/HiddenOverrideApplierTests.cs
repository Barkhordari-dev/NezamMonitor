using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.Tests;

/// <summary>TEST-002: HiddenOverrideApplier — permanent overrides survive a
/// sync, temporary ones are deleted by ApplyAll, and an invalid override
/// is ignored silently (never throws, never breaks the sync/import).</summary>
public class HiddenOverrideApplierTests
{
    private static (NezamDatabase Db, long SnapId) SeededDb(TestHelpers.TempDirectory tmp)
    {
        var db = new NezamDatabase(tmp.File("test.db"));
        var snapId = TestHelpers.SeedActiveSnapshot(db);
        return (db, snapId);
    }

    [Fact]
    public void ApplyAll_PermanentOverride_AppliesAndSurvives()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var (db, snapId) = SeededDb(tmp);
        using var _ = db;

        db.SaveHiddenOverride(new HiddenOverride
        {
            RecordType = "case", RecordKey = "1404/514", Field = "Owner",
            Value = "نام جدید", Mode = "perm", UpdatedAt = HiddenOverrideApplier.NowJalali()
        });

        HiddenOverrideApplier.ApplyAll(db.Connection);

        var cases = db.LoadCases(snapId);
        Assert.Single(cases);
        Assert.Equal("نام جدید", cases[0].Owner);

        // The permanent override is still there after applying (it must
        // re-apply after every future sync/import too).
        Assert.Single(db.GetHiddenOverrides());
    }

    [Fact]
    public void ApplyAll_TemporaryOverride_IsDeletedAfterApplying()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var (db, snapId) = SeededDb(tmp);
        using var _ = db;

        db.SaveHiddenOverride(new HiddenOverride
        {
            RecordType = "case", RecordKey = "1404/514", Field = "Owner",
            Value = "نام موقت", Mode = "temp", UpdatedAt = HiddenOverrideApplier.NowJalali()
        });
        db.SaveHiddenOverride(new HiddenOverride
        {
            RecordType = "case", RecordKey = "1404/514", Field = "OwnerMobile",
            Value = "09999999999", Mode = "perm", UpdatedAt = HiddenOverrideApplier.NowJalali()
        });

        HiddenOverrideApplier.ApplyAll(db.Connection);

        // The temp override was applied once, then deleted; the perm one stays.
        var remaining = db.GetHiddenOverrides();
        Assert.Single(remaining);
        Assert.Equal("perm", remaining[0].Mode);
        Assert.Equal("OwnerMobile", remaining[0].Field);

        var cases = db.LoadCases(snapId);
        Assert.Equal("نام موقت", cases[0].Owner);
        Assert.Equal("09999999999", cases[0].OwnerMobile);
    }

    [Fact]
    public void ApplyOne_FeeAmount_UpdatesTheFeeRow()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var (db, snapId) = SeededDb(tmp);
        using var _ = db;

        var applied = HiddenOverrideApplier.ApplyOne(db.Connection, new HiddenOverride
        {
            RecordType = "fee", RecordKey = "1404/514|۱", Field = "Amount",
            Value = "۹۹۹", Mode = "perm", UpdatedAt = HiddenOverrideApplier.NowJalali()
        });

        Assert.True(applied);
        var cases = db.LoadCases(snapId);
        Assert.Single(cases[0].Fees);
        Assert.Equal("۹۹۹", cases[0].Fees[0].Amount);
    }

    [Fact]
    public void ApplyOne_InvalidRecordTypeOrField_IsIgnoredWithoutThrowing()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var (db, _) = SeededDb(tmp);
        using var _ = db;

        Assert.False(HiddenOverrideApplier.ApplyOne(db.Connection, new HiddenOverride
        {
            RecordType = "bogus", RecordKey = "1404/514", Field = "Owner",
            Value = "x", Mode = "perm", UpdatedAt = HiddenOverrideApplier.NowJalali()
        }));
        Assert.False(HiddenOverrideApplier.ApplyOne(db.Connection, new HiddenOverride
        {
            RecordType = "case", RecordKey = "1404/514", Field = "NoSuchField",
            Value = "x", Mode = "perm", UpdatedAt = HiddenOverrideApplier.NowJalali()
        }));

        // A bogus override stored in the table must not break ApplyAll either.
        db.SaveHiddenOverride(new HiddenOverride
        {
            RecordType = "bogus", RecordKey = "1404/514", Field = "Owner",
            Value = "x", Mode = "perm", UpdatedAt = HiddenOverrideApplier.NowJalali()
        });
        var ex = Record.Exception(() => HiddenOverrideApplier.ApplyAll(db.Connection));
        Assert.Null(ex);
    }
}
