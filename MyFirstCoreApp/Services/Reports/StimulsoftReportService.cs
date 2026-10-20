using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace MyFirstCoreApp.Services.Reports;

public class StimulsoftReportService
{
    public byte[] GenerateTestPdf()
    {
        var report = new StiReport();

        report.Pages.Clear();

        var page = new StiPage(report)
        {
            Name = "Page1",
            PageWidth = 21,
            PageHeight = 29.7,
            Margins = new Stimulsoft.Report.Components.StiMargins(0, 0, 0, 0)
        };

        report.Pages.Add(page);

        // Create a text component
        var title = new StiText
        {
            Text = "Stimulsoft PDF Test",
            Left = 1,
            Top = 1,
            Width = 18,
            Height = 2,
            Font = new System.Drawing.Font(
                "Arial",
                18,
                System.Drawing.FontStyle.Bold)
        };

        page.Components.Add(title);

        // Render before exporting
        report.Render(false);

        using var stream = new MemoryStream();

        report.ExportDocument(
            Stimulsoft.Report.StiExportFormat.Pdf,
            stream);

        return stream.ToArray();
    }
}