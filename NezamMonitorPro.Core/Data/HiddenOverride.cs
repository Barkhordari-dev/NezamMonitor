namespace NezamMonitorPro.Core.Data;

/// <summary>
/// One hidden-menu field override (mirrors Android's HiddenOverrideEntity).
/// RecordKey identifies the record without a DB id (ids change on sync/import):
///  - case: CaseNumber
///  - fee: CaseNumber + "|" + stage number ("1400/123|1")
///  - engineer: CaseNumber + "|" + name
/// Mode is "perm" (re-applied after every sync/import) or "temp" (applied
/// once; deleted after the next sync/import so the server value wins).
/// </summary>
public sealed class HiddenOverride
{
    public long Id { get; set; }
    public string RecordType { get; set; } = "";
    public string RecordKey { get; set; } = "";
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
    public string Mode { get; set; } = "perm";
    public string UpdatedAt { get; set; } = "";
}
