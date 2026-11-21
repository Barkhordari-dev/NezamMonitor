namespace NezamMonitorPro.Core.Data;

// دفترچه دفاتر طراحی و مهندسین یزد (پورت اندروید v3.10.0 تا v3.11.6):
// داده‌ها فقط از اسنپ‌شات داخلی می‌آیند و هیچ به‌روزرسانی درون‌برنامه‌ای
// ندارند. siteId همان شناسهٔ عددی خود سایت است (عددِ /resume/daftar/{id}
// و /resume/engineers/{id})؛ با شمارهٔ دفترِ نمایشی فرق می‌کند
// («دفتر 4» شناسهٔ سایت 32 است). رشتهٔ هیچ رشتهٔ تحصیلی/شغلی‌ای
// اینجا هاردکد نشده؛ همه از دادهٔ اسنپ‌شات می‌آیند.

/// <summary>یک دفتر طراحی (یک ردیف از directory_offices).</summary>
public sealed record DirectoryOffice(
    int SiteId,
    string Name = "",
    string Licence = "",
    string Manager = "",
    string Mobile = "",
    string Contact = "",
    string Province = "",
    string City = "",
    string Address = "",
    string Phone = "",
    string Fax = "",
    string Hours = "",
    string DetailUrl = "",
    int MemberCount = 0,
    string UpdatedAt = "");

/// <summary>یک مهندس دفترچه (یک ردیف از directory_engineers). با مهندسِ
/// هر پرونده فرق می‌کند: این یکی پایه‌ها، تاریخ‌های پروانه و عکس دارد.</summary>
public sealed record DirectoryEngineer(
    int SiteId,
    string Name = "",
    string Family = "",
    string Discipline = "",
    string Phone = "",
    string DesignGrade = "",
    string SupervisionGrade = "",
    string ExecutionGrade = "",
    string LicenceIssue = "",
    string LicenceValidity = "",
    string Photo = "",
    int OfficeId = 0,
    string OfficeName = "",
    string UpdatedAt = "");

/// <summary>یک ردیف سوابق تحصیلی مهندس (directory_education).</summary>
public sealed record DirectoryEducation(
    long Id,
    int EngineerId,
    string Degree = "",
    string Field = "",
    string Orientation = "",
    string UniversityType = "",
    string University = "",
    string City = "",
    string Country = "",
    string Start = "",
    string End = "");

/// <summary>یک ردیف سابقهٔ پروانهٔ مهندس — طراحی/نظارت/اجرا ضربدر پایهٔ
/// ۱/۲/۳ (directory_licence_history).</summary>
public sealed record DirectoryLicenceHistory(
    long Id,
    int EngineerId,
    string Kind = "",
    string Grade1 = "",
    string Grade2 = "",
    string Grade3 = "");
