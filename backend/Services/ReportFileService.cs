using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;

namespace EducationCrm.Api.Services;

public static class ReportFileService
{
    public static byte[] CreateCsv(IReadOnlyCollection<ReportExportRow> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var header in Headers()) csv.WriteField(header);
            csv.NextRecord();
            foreach (var row in rows)
            {
                foreach (var value in Values(row)) csv.WriteField(SafeSpreadsheetText(value));
                csv.NextRecord();
            }
        }
        return stream.ToArray();
    }

    public static byte[] CreateWorkbook(IReadOnlyCollection<ReportExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Reports");
        var headers = Headers();
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
            worksheet.Cell(1, column + 1).Style.Font.Bold = true;
            worksheet.Cell(1, column + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9F0FF");
        }

        var rowNumber = 2;
        foreach (var row in rows)
        {
            var values = Values(row);
            for (var column = 0; column < values.Length; column++)
            {
                worksheet.Cell(rowNumber, column + 1).SetValue(SafeSpreadsheetText(values[column]));
            }
            rowNumber++;
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        worksheet.Columns().AdjustToContents(8, 44);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string[] Headers() => ["Section", "Name", "Total", "Won", "Lost", "Open", "Scheduled Follow-ups", "Completed Follow-ups", "Overdue Follow-ups", "Conversion Rate"];

    private static string[] Values(ReportExportRow row) =>
    [
        row.Section,
        row.Name,
        row.Total.ToString(CultureInfo.InvariantCulture),
        row.Won.ToString(CultureInfo.InvariantCulture),
        row.Lost.ToString(CultureInfo.InvariantCulture),
        row.Open.ToString(CultureInfo.InvariantCulture),
        row.ScheduledFollowUps.ToString(CultureInfo.InvariantCulture),
        row.CompletedFollowUps.ToString(CultureInfo.InvariantCulture),
        row.OverdueFollowUps.ToString(CultureInfo.InvariantCulture),
        row.ConversionRate.ToString("0.0", CultureInfo.InvariantCulture)
    ];

    private static string SafeSpreadsheetText(string? value) =>
        !string.IsNullOrEmpty(value) && "=+-@".Contains(value[0]) ? $"'{value}" : value ?? string.Empty;
}

public sealed record ReportExportRow(
    string Section,
    string Name,
    int Total,
    int Won,
    int Lost,
    int Open,
    int ScheduledFollowUps,
    int CompletedFollowUps,
    int OverdueFollowUps,
    decimal ConversionRate);
