using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Diff;

public enum ChangeType { Added, Removed, Modified, Unchanged }

/// <summary>Field-level difference for a case.</summary>
public sealed record FieldChange(
    string CaseKey,
    string CaseNumber,
    string Owner,
    ChangeType Type,
    string Field,
    string OldValue,
    string NewValue);

/// <summary>Result of comparing two case snapshots.</summary>
public sealed class DiffResult
{
    public List<Case> Added { get; } = new();
    public List<Case> Removed { get; } = new();
    public List<(Case Old, Case New)> Modified { get; } = new();
    public List<Case> Unchanged { get; } = new();
    public List<FieldChange> FieldChanges { get; } = new();

    public int Total => Added.Count + Removed.Count + Modified.Count + Unchanged.Count;
}

/// <summary>
/// Compares case snapshots with order-independent collection comparison.
/// </summary>
public static class SnapshotComparer
{
    public static DiffResult Compare(IReadOnlyList<Case> previous, IReadOnlyList<Case> current)
    {
        var result = new DiffResult();
        var prevByKey = previous.ToDictionary(c => c.Key.ToString(), c => c);
        var currByKey = current.ToDictionary(c => c.Key.ToString(), c => c);

        foreach (var (key, curr) in currByKey)
        {
            if (!prevByKey.TryGetValue(key, out var prev))
            {
                result.Added.Add(curr);
                continue;
            }
            var changes = DiffCase(prev, curr);
            if (changes.Count > 0)
            {
                result.Modified.Add((prev, curr));
                result.FieldChanges.AddRange(changes);
            }
            else
            {
                result.Unchanged.Add(curr);
            }
        }

        foreach (var (key, prev) in prevByKey)
        {
            if (!currByKey.ContainsKey(key))
                result.Removed.Add(prev);
        }

        return result;
    }

    private static List<FieldChange> DiffCase(Case prev, Case curr)
    {
        var key = curr.Key.ToString();
        var changes = new List<FieldChange>();
        void Check(string field, string oldV, string newV)
        {
            var o = TextNormalizer.NormalizeForCompare(oldV);
            var n = TextNormalizer.NormalizeForCompare(newV);
            if (o != n)
                changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, field, oldV ?? "", newV ?? ""));
        }

        Check("Owner", prev.Owner, curr.Owner);
        Check("OwnerMobile", prev.OwnerMobile, curr.OwnerMobile);
        Check("Responsibility", prev.Responsibility, curr.Responsibility);
        Check("CapacityDate", prev.CapacityDate, curr.CapacityDate);
        Check("Office", prev.Office, curr.Office);
        Check("ReportDate1", prev.ReportDate1, curr.ReportDate1);
        Check("ReportDate2", prev.ReportDate2, curr.ReportDate2);
        Check("ReportDate3", prev.ReportDate3, curr.ReportDate3);

        // Specification
        var pSpec = prev.Specification;
        var cSpec = curr.Specification;
        if (pSpec is null && cSpec is not null)
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Specification", "", "(new)"));
        else if (pSpec is not null && cSpec is null)
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Specification", "(old)", ""));
        else if (pSpec is not null && cSpec is not null)
        {
            Check("BuildingGroup", pSpec.BuildingGroup, cSpec.BuildingGroup);
            Check("RenovationCode", pSpec.RenovationCode, cSpec.RenovationCode);
            Check("PlanInstructionNo", pSpec.PlanInstructionNo, cSpec.PlanInstructionNo);
            Check("PlanInstructionType", pSpec.PlanInstructionType, cSpec.PlanInstructionType);
            Check("PlanInstructionDate", pSpec.PlanInstructionDate, cSpec.PlanInstructionDate);
            Check("LandArea", pSpec.LandArea, cSpec.LandArea);
            Check("ParafArea", pSpec.ParafArea, cSpec.ParafArea);
            Check("CapacityArea", pSpec.CapacityArea, cSpec.CapacityArea);
            Check("StructureType", pSpec.StructureType, cSpec.StructureType);
            Check("BlockTitle", pSpec.BlockTitle, cSpec.BlockTitle);
            Check("BlockCount", pSpec.BlockCount, cSpec.BlockCount);
            Check("Floors", pSpec.Floors, cSpec.Floors);
            Check("Units", pSpec.Units, cSpec.Units);
            Check("Issuer", pSpec.Issuer, cSpec.Issuer);
            Check("PermitNumber", pSpec.PermitNumber, cSpec.PermitNumber);
            Check("PermitDate", pSpec.PermitDate, cSpec.PermitDate);
            Check("ReleaseDate", pSpec.ReleaseDate, cSpec.ReleaseDate);
            Check("PlanZone", pSpec.PlanZone, cSpec.PlanZone);
            Check("Address", pSpec.Address, cSpec.Address);
        }

        // Engineers: order-independent set comparison by identity
        var pEng = prev.Engineers.Select(e => e.Identity).ToHashSet();
        var cEng = curr.Engineers.Select(e => e.Identity).ToHashSet();
        foreach (var id in cEng.Where(id => !pEng.Contains(id)))
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Engineer", "", id));
        foreach (var id in pEng.Where(id => !cEng.Contains(id)))
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Engineer", id, ""));

        // Fees: order-independent by identity+amount
        var pFee = prev.Fees.Select(f => f.Identity + "|" + TextNormalizer.NormalizeForCompare(f.Amount)).ToHashSet();
        var cFee = curr.Fees.Select(f => f.Identity + "|" + TextNormalizer.NormalizeForCompare(f.Amount)).ToHashSet();
        foreach (var id in cFee.Where(id => !pFee.Contains(id)))
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Fee", "", id));
        foreach (var id in pFee.Where(id => !cFee.Contains(id)))
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Fee", id, ""));

        // Reports: order-independent by identity
        var pRep = prev.Reports.Select(r => r.Identity).ToHashSet();
        var cRep = curr.Reports.Select(r => r.Identity).ToHashSet();
        foreach (var id in cRep.Where(id => !pRep.Contains(id)))
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Report", "", id));
        foreach (var id in pRep.Where(id => !cRep.Contains(id)))
            changes.Add(new FieldChange(key, curr.CaseNumber, curr.Owner, ChangeType.Modified, "Report", id, ""));

        return changes;
    }
}
