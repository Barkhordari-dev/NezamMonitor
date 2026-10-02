using NezamMonitor.Core.Api;
using NezamMonitor.Core.Models;
using Xunit;

namespace NezamMonitor.Tests;

public class ApiExtractorTests
{
    [Fact]
    public void MapCase_ExtractsAllFields()
    {
        var api = new Dictionary<string, object?>
        {
            ["das_serial"] = 4565,
            ["das_year"] = 1404,
            ["das_number"] = 514,
            ["own_name"] = "سميرا",
            ["own_famil"] = "رجبي",
            ["own_mob"] = "09196842392",
            ["mas_title"] = "مكانيك",
            ["kasr_date"] = "1404/10/30",
            ["daf_name"] = "شماره205يزد",
            ["bg_name"] = "الف",
            ["das_code_nosazi"] = "000124202110000",
            ["das_shahrdari_num"] = "3247",
            ["dastyp_title"] = "احداثي",
            ["das_masahat"] = "200",
            ["db_metraj_shahrdari"] = "145.4",
            ["db_metraj_effective_nezarat"] = "145.4",
            ["sazetyp_title"] = "آجري",
            ["db_title"] = "بلوك1",
            ["das_blocknum"] = "1",
            ["db_tabagha_shahrdari"] = "1",
            ["db_vahed_count"] = "1",
            ["das_sad_title"] = "شهرداري اردكان",
            ["das_parvana_num"] = "1404/0805",
            ["das_parvana_date"] = "1404/11/19",
            ["das_date_tarkhis"] = "1404/11/13",
            ["mah_title"] = "بافت جديد",
            ["das_address"] = "شهرك طوس",
            ["usetyp_title"] = "مسكوني",
            ["db_id"] = 3817,
        };

        var result = ApiExtractor.MapCase(api);

        Assert.Equal("1404/514", result.CaseNumber);
        Assert.Equal("4565", result.Serial);
        Assert.Equal("سميرا رجبي", result.Owner);
        Assert.Equal("09196842392", result.OwnerMobile);
        Assert.Equal("مكانيك", result.Responsibility);
        Assert.Equal("1404/10/30", result.CapacityDate);
        Assert.Equal("شماره205يزد", result.Office);
        Assert.NotNull(result.Specification);

        var spec = result.Specification!;
        Assert.Equal("الف", spec.BuildingGroup);
        Assert.Equal("000124202110000", spec.RenovationCode);
        Assert.Equal("3247", spec.PlanInstructionNo);
        Assert.Equal("احداثي", spec.PlanInstructionType);
        Assert.Equal("200", spec.LandArea);
        Assert.Equal("145.4", spec.ParafArea);
        Assert.Equal("145.4", spec.CapacityArea);
        Assert.Equal("آجري", spec.StructureType);
        Assert.Equal("بلوك1", spec.BlockTitle);
        Assert.Equal("1", spec.Floors);
        Assert.Equal("1", spec.Units);
        Assert.Equal("شهرداري اردكان", spec.Issuer);
        Assert.Equal("1404/0805", spec.PermitNumber);
        Assert.Equal("1404/11/19", spec.PermitDate);
        Assert.Equal("1404/11/13", spec.ReleaseDate);
        Assert.Equal("بافت جديد", spec.PlanZone);
        Assert.Equal("شهرك طوس", spec.Address);
        Assert.Equal("مسكوني", spec.UsageType);
    }

    [Fact]
    public void MapEngineers_MapsAllFields()
    {
        var apiList = new List<Dictionary<string, object?>>
        {
            new() { ["mas_title"] = "معماري", ["ozh_name"] = "مهديه", ["ozh_famil"] = "قپاني" },
            new() { ["mas_title"] = "عمران", ["ozh_name"] = "مهديه", ["ozh_famil"] = "قپاني" },
            new() { ["mas_title"] = "مكانيك", ["ozh_name"] = "محمدمهدي", ["ozh_famil"] = "برخورداري" },
            new() { ["mas_title"] = "برق", ["ozh_name"] = "حميده", ["ozh_famil"] = "ميرجاني سروي" },
            new() { ["mas_title"] = "هماهنگ كننده", ["ozh_name"] = "مهديه", ["ozh_famil"] = "قپاني" },
        };

        var result = ApiExtractor.MapEngineers(apiList);

        Assert.Equal(5, result.Count);
        Assert.Contains(result, e => e.Discipline == "معماري" && e.Name.Contains("قپاني"));
        Assert.Contains(result, e => e.Discipline == "عمران");
        Assert.Contains(result, e => e.Discipline == "مكانيك");
        Assert.Contains(result, e => e.Discipline == "برق");
        Assert.Contains(result, e => e.Discipline == "هماهنگ كننده");
    }

    [Fact]
    public void MapFees_MapsAllFields()
    {
        var apiList = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["mas_title"] = "مكانيك",
                ["dbm_mablagh"] = "23158293",
                ["payed"] = "yes",
                ["dbm_marhala_id"] = "1",
                ["pardakht_date"] = "1405/02/20",
                ["sanad_num"] = "79",
                ["sanad_date"] = "1405/02/01",
                ["dbm_p_type"] = "n",
            }
        };

        var result = ApiExtractor.MapFees(apiList);

        Assert.Single(result);
        Assert.Equal("مكانيك", result[0].Discipline);
        Assert.Contains("23158293", result[0].Amount);
        Assert.Contains("ریال", result[0].Amount);
        Assert.Equal("پرداخت شده", result[0].PayStatus);
        Assert.Equal("1", result[0].Stage);
        Assert.Equal("1405/02/20", result[0].EndDate);
    }

    [Fact]
    public void MapFees_UnpaidStatus()
    {
        var apiList = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["mas_title"] = "مكانيك",
                ["dbm_mablagh"] = "7719431",
                ["payed"] = "no",
                ["dbm_marhala_id"] = "2",
                ["pardakht_date"] = "",
                ["sanad_num"] = "",
                ["sanad_date"] = "",
                ["dbm_p_type"] = "n",
            }
        };

        var result = ApiExtractor.MapFees(apiList);
        Assert.Equal("پرداخت نشده", result[0].PayStatus);
    }

    [Fact]
    public void MapCase_EmptyFields_DoesNotThrow()
    {
        var api = new Dictionary<string, object?>();
        var result = ApiExtractor.MapCase(api);
        Assert.Equal("/", result.CaseNumber);
        Assert.Equal("", result.Owner);
        Assert.NotNull(result.Specification);
    }

    [Fact]
    public void MapEngineers_EmptyList_ReturnsEmpty()
    {
        var result = ApiExtractor.MapEngineers(new());
        Assert.Empty(result);
    }

    [Fact]
    public void MapFees_EmptyList_ReturnsEmpty()
    {
        var result = ApiExtractor.MapFees(new());
        Assert.Empty(result);
    }
}
