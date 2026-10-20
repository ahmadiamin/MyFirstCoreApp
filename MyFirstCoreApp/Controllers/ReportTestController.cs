using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Services.Reports;

namespace MyFirstCoreApp.Controllers;

public class ReportTestController : Controller
{
    private readonly StimulsoftReportService _reportService;

    public ReportTestController(StimulsoftReportService reportService)
    {
        _reportService = reportService;
    }

    public IActionResult Pdf()
    {
        var pdf = _reportService.GenerateTestPdf();

        return File(pdf, "application/pdf");
    }
}
