using ClosedXML.Excel;
using DentalLab.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DentalLab.Services
{
    /// <summary>
    /// تقرير جدولي واحد يمكن تصديره إلى PDF أو Excel.
    /// القيم تبقى بأنواعها (رقم/تاريخ/نص) حتى تكون الأرقام قابلة للحساب في Excel.
    /// </summary>
    public class ReportTable
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }

        /// <summary>بيانات رأس التقرير (مثلاً: المركز، الفترة، تاريخ الإصدار).</summary>
        public List<(string Label, string Value)> Info { get; set; } = [];

        public string[] Headers { get; set; } = [];

        /// <summary>عرض نسبي لكل عمود في PDF (اختياري).</summary>
        public float[]? ColumnWidths { get; set; }

        public List<object?[]> Rows { get; set; } = [];

        /// <summary>سطر الإجمالي أسفل الجدول (اختياري).</summary>
        public object?[]? Footer { get; set; }

        /// <summary>ملخص أسفل الجدول (مثلاً: المسدد، المتبقي).</summary>
        public List<(string Label, string Value)> Summary { get; set; } = [];

        public string? Note { get; set; }

        /// <summary>اسم الملف بدون امتداد.</summary>
        public string FileName { get; set; } = "report";
    }

    public enum ExportFormat { Pdf, Excel }

    public class ExportService
    {
        private const string PdfFont = "Tahoma";
        private static readonly string[] FontFallbacks = ["Arial", "Segoe UI"];

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ExportService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public static ExportFormat ParseFormat(string? format) =>
            string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
                ? ExportFormat.Excel
                : ExportFormat.Pdf;

        /// <summary>يبني الملف ويعيد (المحتوى، نوع المحتوى، اسم الملف).</summary>
        public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(ReportTable report, ExportFormat format)
        {
            if (format == ExportFormat.Excel)
            {
                return (BuildExcel(report),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{report.FileName}.xlsx");
            }

            var site = await _context.SiteInfo.AsNoTracking().FirstOrDefaultAsync();
            var logo = TryLoadLogo(site?.LogoUrl);
            return (BuildPdf(report, site?.Name, site?.Phone, logo), "application/pdf", $"{report.FileName}.pdf");
        }

        // ───────────────────────── Excel ─────────────────────────

        private static byte[] BuildExcel(ReportTable report)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(SafeSheetName(report.Title));
            ws.RightToLeft = true;

            var cols = Math.Max(1, report.Headers.Length);
            var row = 1;

            ws.Cell(row, 1).Value = report.Title;
            ws.Range(row, 1, row, cols).Merge().Style.Font.SetBold().Font.SetFontSize(14);
            row++;

            if (!string.IsNullOrWhiteSpace(report.Subtitle))
            {
                ws.Cell(row, 1).Value = report.Subtitle;
                ws.Range(row, 1, row, cols).Merge();
                row++;
            }

            foreach (var (label, value) in report.Info)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 1).Style.Font.SetBold();
                ws.Cell(row, 2).Value = value;
                row++;
            }

            if (report.Info.Count > 0 || !string.IsNullOrWhiteSpace(report.Subtitle))
                row++;

            var headerRow = row;
            for (var c = 0; c < report.Headers.Length; c++)
                ws.Cell(row, c + 1).Value = report.Headers[c];

            var header = ws.Range(row, 1, row, cols);
            header.Style.Font.SetBold();
            header.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E9ECEF"));
            row++;

            foreach (var values in report.Rows)
            {
                WriteExcelRow(ws, row, values);
                row++;
            }

            if (report.Footer != null)
            {
                WriteExcelRow(ws, row, report.Footer);
                var footer = ws.Range(row, 1, row, cols);
                footer.Style.Font.SetBold();
                footer.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8F9FA"));
                row++;
            }

            ws.Range(headerRow, 1, row - 1, cols).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin);

            if (report.Summary.Count > 0)
            {
                row++;
                foreach (var (label, value) in report.Summary)
                {
                    ws.Cell(row, 1).Value = label;
                    ws.Cell(row, 1).Style.Font.SetBold();
                    ws.Cell(row, 2).Value = value;
                    row++;
                }
            }

            if (!string.IsNullOrWhiteSpace(report.Note))
            {
                row++;
                ws.Cell(row, 1).Value = report.Note;
            }

            ws.SheetView.FreezeRows(headerRow);
            ws.Columns(1, cols).AdjustToContents(headerRow, row, 8, 60);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void WriteExcelRow(IXLWorksheet ws, int row, object?[] values)
        {
            for (var c = 0; c < values.Length; c++)
            {
                var cell = ws.Cell(row, c + 1);
                switch (values[c])
                {
                    case null:
                        break;
                    case decimal d:
                        cell.Value = d;
                        cell.Style.NumberFormat.Format = "#,##0.00";
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    case double db:
                        cell.Value = db;
                        break;
                    case DateTime dt:
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "dd/MM/yyyy";
                        break;
                    case bool b:
                        cell.Value = b ? "نعم" : "";
                        break;
                    default:
                        cell.Value = values[c]!.ToString();
                        break;
                }
            }
        }

        private static string SafeSheetName(string title)
        {
            var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            var clean = new string(title.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
            if (clean.Length == 0) clean = "Report";
            return clean.Length > 31 ? clean[..31] : clean;
        }

        // ───────────────────────── PDF ─────────────────────────

        public static string FormatCell(object? value) => value switch
        {
            null => string.Empty,
            decimal d => d.ToString("N2"),
            double db => db.ToString("N2"),
            DateTime dt => dt.ToString("dd/MM/yyyy"),
            bool b => b ? "نعم" : "",
            _ => value.ToString() ?? string.Empty
        };

        private static byte[] BuildPdf(ReportTable report, string? labName, string? labPhone, byte[]? logo)
        {
            var landscape = report.Headers.Length > 7;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                    page.Margin(25);
                    page.ContentFromRightToLeft();
                    page.DefaultTextStyle(t => t.FontFamily([PdfFont, .. FontFallbacks]).FontSize(9));

                    page.Header().PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            if (!string.IsNullOrWhiteSpace(labName))
                                col.Item().Text(labName).FontSize(14).Bold();
                            if (!string.IsNullOrWhiteSpace(labPhone))
                                col.Item().Text($"هاتف: {labPhone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            col.Item().PaddingTop(4).Text(report.Title).FontSize(13).Bold().FontColor(Colors.Blue.Darken3);
                            if (!string.IsNullOrWhiteSpace(report.Subtitle))
                                col.Item().Text(report.Subtitle).FontSize(10);
                        });

                        if (logo != null)
                            row.ConstantItem(70).Height(55).AlignLeft().Image(logo).FitArea();
                    });

                    page.Content().Column(col =>
                    {
                        if (report.Info.Count > 0)
                        {
                            col.Item().PaddingBottom(8).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(2);
                                });

                                foreach (var (label, value) in report.Info)
                                {
                                    t.Cell().PaddingVertical(2).Text(label).Bold();
                                    t.Cell().PaddingVertical(2).Text(value);
                                }
                            });
                        }

                        col.Item().Table(table =>
                        {
                            var widths = report.ColumnWidths ?? report.Headers.Select(_ => 1f).ToArray();
                            table.ColumnsDefinition(c =>
                            {
                                foreach (var w in widths)
                                    c.RelativeColumn(w);
                            });

                            table.Header(h =>
                            {
                                foreach (var title in report.Headers)
                                    h.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium)
                                        .Padding(4).AlignCenter().Text(title).Bold();
                            });

                            var stripe = false;
                            foreach (var values in report.Rows)
                            {
                                var bg = stripe ? Colors.Grey.Lighten5 : Colors.White;
                                stripe = !stripe;
                                for (var i = 0; i < report.Headers.Length; i++)
                                {
                                    var v = i < values.Length ? values[i] : null;
                                    var cell = table.Cell().Background(bg).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3);
                                    (IsNumeric(v) ? cell.AlignCenter() : cell).Text(FormatCell(v));
                                }
                            }

                            if (report.Footer != null)
                            {
                                for (var i = 0; i < report.Headers.Length; i++)
                                {
                                    var v = i < report.Footer.Length ? report.Footer[i] : null;
                                    table.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Medium)
                                        .Padding(4).AlignCenter().Text(FormatCell(v)).Bold();
                                }
                            }
                        });

                        if (report.Summary.Count > 0)
                        {
                            col.Item().PaddingTop(10).AlignLeft().Width(250).Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(3);
                                    c.RelativeColumn(2);
                                });
                                foreach (var (label, value) in report.Summary)
                                {
                                    t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(label).Bold();
                                    t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).AlignCenter().Text(value);
                                }
                            });
                        }

                        if (!string.IsNullOrWhiteSpace(report.Note))
                            col.Item().PaddingTop(10).Text(report.Note).FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"تاريخ الطباعة: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignLeft().Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                            t.Span("صفحة ");
                            t.CurrentPageNumber();
                            t.Span(" من ");
                            t.TotalPages();
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static bool IsNumeric(object? v) => v is decimal or int or double or DateTime;

        /// <summary>الشعار من مجلد الرفع؛ PDF يدعم PNG/JPG فقط، وأي خطأ يعني تقريراً بدون شعار.</summary>
        private byte[]? TryLoadLogo(string? logoUrl)
        {
            if (string.IsNullOrWhiteSpace(logoUrl))
                return null;

            try
            {
                var ext = Path.GetExtension(logoUrl).ToLowerInvariant();
                if (ext is not (".png" or ".jpg" or ".jpeg"))
                    return null;

                var relative = logoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var uploadRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "upload"));
                var full = Path.GetFullPath(Path.Combine(uploadRoot, relative));
                if (!full.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                    return null;

                return File.ReadAllBytes(full);
            }
            catch
            {
                return null;
            }
        }
    }
}
