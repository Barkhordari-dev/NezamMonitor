using NezamMonitor.Core;
using NezamMonitor.Core.Parsing;
using Xunit.Abstractions;

namespace NezamMonitor.Tests;

public class DebugParserTests
{
    private readonly ITestOutputHelper _out;
    public DebugParserTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Debug_AllPatterns()
    {
        var text = """
            ناظرین پرونده
            بلوك1
            معماري :
            مهديه قپاني
            عمران :
            مهديه قپاني
            مكانيك :
            محمدمهدي برخورداري
            برق :
            حميده ميرجاني سروي
            هماهنگ كننده :
            مهديه قپاني
            """;
        var engineers = EngineerParser.Parse(text);
        _out.WriteLine($"Count: {engineers.Count}");
        foreach (var e in engineers)
            _out.WriteLine($"  [{e.Discipline}] = {e.Name}");
        foreach (var e in engineers)
            _out.WriteLine($"  discipline codes: {string.Join(" ", e.Discipline.Select(c => $"U+{(int)c:X4}"))}");
    }
}
