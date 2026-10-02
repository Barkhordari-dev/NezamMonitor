using NezamMonitor.Core.Parsing;
using NezamMonitor.Core;
using Xunit;

namespace NezamMonitor.Tests;

public class ParserTests
{
    private const string EngineerDialogText = """
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

    [Fact]
    public void EngineerParser_ExtractsAllFiveDisciplines()
    {
        var engineers = EngineerParser.Parse(EngineerDialogText);
        Assert.Equal(5, engineers.Count);
        Assert.Contains(engineers, e => e.Discipline == "معماری" && e.Name.Contains("قپانی"));
        Assert.Contains(engineers, e => e.Discipline == "عمران");
        Assert.Contains(engineers, e => e.Discipline == "مکانیک" && e.Name.Contains("برخورداری"));
        Assert.Contains(engineers, e => e.Discipline == "برق" && e.Name.Contains("میرجانی"));
        Assert.Contains(engineers, e => e.Discipline == "هماهنگ‌کننده");
    }

    [Fact]
    public void EngineerParser_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(EngineerParser.Parse(""));
    }

    private const string SpecDialogText = """
        4565 - مسكوني
        گروه ساختمانی :
        الف
        کد نوسازی :
        ۰۰۰۱۲۴۲۰۲۱۱۰۰۰۰
        شماره دستور تهیه نقشه :
        ۳۲۴۷
        نوع دستور تهیه نقشه :
        احداثي
        مساحت زمین :
        ۲۰۰
        متراژ پاراف :
        ۱۴۵.۴ مترمربع
        متراژ کسر ظرفیت نظارت :
        ۱۴۵.۴ مترمربع
        تاریخ دستور تهیه نقشه :
        ۱۴۰۴/۰۹/۰۹
        نوع سازه :
        آجري
        عنوان بلوک :
        بلوك۱
        تعداد بلوک :
        ۱
        تعداد طبقات :
        ۱
        تعداد واحد :
        ۱
        صادر کننده :
        شهرداري اردكان
        شماره پروانه :
        ۱۴۰۴/۰۸۰۵
        تاریخ صدور پروانه :
        ۱۴۰۴/۱۱/۱۹
        تاریخ ترخیص :
        ۱۴۰۴/۱۱/۱۳
        محدوده طرح :
        بافت جديد
        آدرس :
        شهرك طوس
        """;

    [Fact]
    public void SpecificationParser_ExtractsAllFields()
    {
        var spec = SpecificationParser.Parse(SpecDialogText);
        Assert.Equal("الف", spec.BuildingGroup);
        Assert.Equal("۰۰۰۱۲۴۲۰۲۱۱۰۰۰۰", spec.RenovationCode);
        Assert.Equal("۳۲۴۷", spec.PlanInstructionNo);
        Assert.Equal("احداثی", spec.PlanInstructionType);
        Assert.Equal("۲۰۰", spec.LandArea);
        Assert.Contains("۱۴۵.۴", spec.ParafArea);
        Assert.Equal("آجری", spec.StructureType);
        Assert.Equal("بلوک۱", spec.BlockTitle);
        Assert.Equal("۱", spec.BlockCount);
        Assert.Equal("۱", spec.Floors);
        Assert.Equal("۱", spec.Units);
        Assert.Contains("اردکان", spec.Issuer);
        Assert.Equal("۱۴۰۴/۰۸۰۵", spec.PermitNumber);
        Assert.Equal("۱۴۰۴/۱۱/۱۹", spec.PermitDate);
        Assert.Equal("۱۴۰۴/۱۱/۱۳", spec.ReleaseDate);
        Assert.Equal("بافت جدید", spec.PlanZone);
        Assert.Contains("طوس", spec.Address);
    }

    [Fact]
    public void SpecificationParser_PermitReverse_IsCorrect()
    {
        var spec = SpecificationParser.Parse(SpecDialogText);
        Assert.Equal("0805-1404", DigitNormalizer.ReverseForFilename(spec.PermitNumber));
    }

    [Fact]
    public void FeeParser_ExtractsPaymentStages()
    {
        var feeText = string.Join('\n',
            "مكانيك\tنظارت\t۷۹\t۱۴۰۵/۰۲/۰۱\t۱۴۰۵/۰۲/۲۰\t۲۳,۱۵۸,۲۹۳ ریال\t۱\tپرداخت شده\tتایید شده\tنظارت",
            "مكانيك\tنظارت\t۸۲\t۱۴۰۵/۰۵/۰۴\t_\t۷,۷۱۹,۴۳۱ ریال\t۲\tتایید شده\tنظارت",
            "مكانيك\tنظارت\t_\t_\t_\t۱۰۴,۶۶۴ ریال\t۰\t_\t_\tبیمه",
            "مكانيك\tنظارت\t_\t_\t_\t۴,۵۸۰,۰۹۹ ریال\t۰\t_\t_\tحسن انجام کار");
        var fees = FeeParser.Parse(feeText);
        Assert.Equal(4, fees.Count);
        Assert.Equal("۱", fees[0].Stage);
        Assert.Contains("۲۳,۱۵۸,۲۹۳", fees[0].Amount);
        Assert.Equal("پرداخت شده", fees[0].PayStatus);
        Assert.Equal("تایید شده", fees[0].ConfirmStatus);
        Assert.Equal("نظارت", fees[0].AmountType);
        Assert.Equal("بیمه", fees[2].AmountType);
    }

    [Fact]
    public void FeeParser_LinesWithoutRial_AreSkipped()
    {
        var fees = FeeParser.Parse("header line\nno rial here");
        Assert.Empty(fees);
    }

    [Fact]
    public void ReportParser_ParsesAllRows()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "ردیف", "نوع گزارش", "مرحله", "مهندس ناظر", "مسئولیت", "تاریخ بازدید", "تعداد سقف اجرا شده", "شماره اندیکاتور", "نمایش", "عملیات" },
            new[] { "۱", "معماري", "مرحله اول : اجراي فونداسيون", "مهديه قپاني", "معماري", "۱۴۰۴/۱۲/۰۴", "۱", "_", "", "_" },
            new[] { "۲", "معماري", "مرحله اول : اجراي فونداسيون", "مهديه قپاني", "معماري", "۱۴۰۴/۱۲/۰۴", "۱", "_", "", "_" },
            new[] { "۷", "تأسيسات برق", "مرحله اول", "حميده ميرجاني سروي", "برق", "۱۴۰۵/۰۱/۲۵", "۱", "_", "", "_" },
        };
        var reports = ReportParser.ParseRows(rows);
        Assert.Equal(3, reports.Count);
        Assert.Equal("۱", reports[0].RowNo);
        Assert.Equal("معماري", reports[0].ReportType);
        Assert.Contains("فونداسيون", reports[0].Stage);
        Assert.Equal("مهديه قپاني", reports[0].Engineer);
        Assert.Equal("۱۴۰۴/۱۲/۰۴", reports[0].VisitDate);
        Assert.Equal("برق", reports[2].Discipline);
    }

    [Fact]
    public void ReportParser_SkipsHeaderAndNonNumericRows()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "ردیف", "نوع گزارش", "مرحله", "x", "x", "x", "x", "x", "x", "x" },
            new[] { "", "", "", "", "", "", "", "", "", "" },
            new[] { "۵", "معماري", "مرحله اول", "ناظر", "معماري", "۱۴۰۴/۱۲/۰۴", "۱", "_", "", "_" },
        };
        var reports = ReportParser.ParseRows(rows);
        Assert.Single(reports);
        Assert.Equal("۵", reports[0].RowNo);
    }
}
