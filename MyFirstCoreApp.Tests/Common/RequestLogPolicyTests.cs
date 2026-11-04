using MyFirstCoreApp.Application.Common.Logging;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class RequestLogPolicyTests
{
    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void ServerErrors_AreLoggedAsError(int status)
        => Assert.Equal(RequestLogSeverity.Error, RequestLogPolicy.Classify(status, false, "/Invoicing/Invoices", 40));

    [Fact]
    public void AnUnhandledException_IsAnError_EvenWithA200Status()
    {
        // وقتی Exception هست، وضعیت پاسخ هنوز 200 است (صفحه‌ی خطا بعداً ساخته می‌شود)
        Assert.Equal(RequestLogSeverity.Error, RequestLogPolicy.Classify(200, true, "/Dashboard", 10));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public void AccessDenied_IsAWarning_SoUnauthorizedAttemptsStandOut(int status)
    {
        Assert.Equal(RequestLogSeverity.Warning, RequestLogPolicy.Classify(status, false, "/Admin/Users", 5));

        // حتی برای فایل ثابت
        Assert.Equal(RequestLogSeverity.Warning, RequestLogPolicy.Classify(status, false, "/lib/secret.js", 5));
    }

    [Theory]
    [InlineData("/lib/jquery/jquery.min.js")]
    [InlineData("/css/site.css")]
    [InlineData("/plugins/chart.js/Chart.min.js")]
    [InlineData("/dist/img/AdminLTELogo.png")]
    [InlineData("/favicon.ico")]
    [InlineData("/fonts/vazir.woff2")]
    [InlineData("/something/unknown/photo.JPG")]
    public void SuccessfulStaticAssets_AreVerbose_SoTheyNeverClutterTheLog(string path)
        => Assert.Equal(RequestLogSeverity.Verbose, RequestLogPolicy.Classify(200, false, path, 3));

    [Fact]
    public void AMissingStaticAsset_IsStillVisible()
    {
        // 404 برای فایل ثابت (لینک شکسته) نباید پنهان شود
        Assert.Equal(RequestLogSeverity.Information, RequestLogPolicy.Classify(404, false, "/css/missing.css", 3));
    }

    [Theory]
    [InlineData("/Dashboard")]
    [InlineData("/Invoicing/Invoices/Post")]
    [InlineData("/Accounting/Reports/Debtors")]
    [InlineData("/")]
    public void NormalPages_AreInformation(string path)
        => Assert.Equal(RequestLogSeverity.Information, RequestLogPolicy.Classify(200, false, path, 120));

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    public void ClientErrors_AreInformation_NotNoisyWarnings(int status)
        => Assert.Equal(RequestLogSeverity.Information, RequestLogPolicy.Classify(status, false, "/Nope", 12));

    [Fact]
    public void SlowRequests_AreWarnings_ButOnlyAboveTheThreshold()
    {
        Assert.Equal(RequestLogSeverity.Information,
            RequestLogPolicy.Classify(200, false, "/Accounting/Reports/Ledger", RequestLogPolicy.SlowRequestThresholdMs));

        Assert.Equal(RequestLogSeverity.Warning,
            RequestLogPolicy.Classify(200, false, "/Accounting/Reports/Ledger", RequestLogPolicy.SlowRequestThresholdMs + 1));
    }

    [Fact]
    public void SlowStaticAssets_StayVerbose()
        => Assert.Equal(RequestLogSeverity.Verbose, RequestLogPolicy.Classify(200, false, "/lib/big.js", 9_999));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("/Dashboard", false)]
    [InlineData("/Contacts/Index", false)]
    [InlineData("/lib/x", true)]
    [InlineData("/LIB/x", true)]               // حروف بزرگ و کوچک فرقی ندارد
    [InlineData("/a/b.css", true)]
    [InlineData("/a/b.CSS", true)]
    [InlineData("/Attachments/Download/5", false)]   // دانلود پیوست، فایل ثابت نیست
    public void IsStaticAsset_RecognisesPrefixesAndExtensions(string? path, bool expected)
        => Assert.Equal(expected, RequestLogPolicy.IsStaticAsset(path));
}
