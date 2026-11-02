using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Services;
using DentalLab.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// الفواتير الشهرية. تُبنى من أسطر الحالات، فلا يمكن أن تختلف فاتورة المركز
    /// عن مجموع فواتير أطبائه.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class InvoicesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly InvoiceService _invoices;
        private readonly FinanceService _finance;

        public InvoicesController(AppDbContext context, InvoiceService invoices, FinanceService finance)
        {
            _context = context;
            _invoices = invoices;
            _finance = finance;
        }

        public async Task<IActionResult> Index(int? year, int? month)
        {
            var now = DateTime.Now;
            int y = year ?? now.Year;
            int m = month ?? now.Month;

            ViewBag.Year = y;
            ViewBag.Month = m;
            ViewBag.AvailableYears = await _finance.GetAvailableYearsAsync();
            ViewBag.MonthClinics = await _invoices.GetMonthClinicsAsync(y, m);

            var issued = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Clinic)
                .Include(i => i.Doctor)
                .Include(i => i.Payments)
                .Where(i => i.Year == y && i.Month == m)
                .OrderBy(i => i.Number)
                .ToListAsync();

            return View(issued);
        }

        [HttpGet]
        public async Task<IActionResult> Preview(Guid? clinicId, Guid? doctorId, int year, int month)
        {
            var vm = await _invoices.BuildPreviewAsync(clinicId, doctorId, year, month);

            ViewBag.Doctors = new SelectList(
                await _context.Doctors.AsNoTracking().OrderBy(d => d.Name).ToListAsync(),
                "Id", "Name", doctorId);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(Guid? clinicId, Guid? doctorId, int year, int month, string? notes)
        {
            var invoice = await _invoices.IssueAsync(clinicId, doctorId, year, month, notes);

            if (invoice == null)
            {
                TempData["ErrorMessage"] = "لا توجد أعمال غير مفوترة في هذه الفترة — لم تُصدر أي فاتورة.";
                return RedirectToAction(nameof(Index), new { year, month });
            }

            TempData["SuccessMessage"] =
                $"صدرت الفاتورة رقم {invoice.Number} — {invoice.TotalUnits} وحدة بإجمالي {invoice.TotalAmount:N2}";

            return RedirectToAction(nameof(Details), new { id = invoice.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Clinic)
                .Include(i => i.Doctor)
                .Include(i => i.Lines)
                .Include(i => i.Payments).ThenInclude(p => p.FinancialAccount)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            ViewBag.Accounts = new SelectList(
                await _context.FinancialAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                "Id", "Name");
            ViewBag.Methods = new SelectList(PaymentMethod.All);

            return View(invoice);
        }

        /// <summary>نسخة للطباعة بنفس شكل الفواتير الورقية.</summary>
        [HttpGet]
        public async Task<IActionResult> Print(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Clinic)
                .Include(i => i.Doctor)
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            ViewBag.Site = await _context.SiteInfo.AsNoTracking().FirstOrDefaultAsync();
            return View(invoice);
        }

        /// <summary>تصدير الفاتورة إلى PDF أو Excel.</summary>
        [HttpGet]
        public async Task<IActionResult> Export(Guid? id, string? format, [FromServices] ExportService export)
        {
            if (id == null)
                return View("NotFound");

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Clinic)
                .Include(i => i.Doctor)
                .Include(i => i.Lines)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            var site = await _context.SiteInfo.AsNoTracking().FirstOrDefaultAsync();

            var report = new ReportTable
            {
                Title = $"فاتورة رقم {invoice.Number}",
                Subtitle = $"عن شهر {invoice.Month}/{invoice.Year}",
                FileName = $"Invoice-{invoice.Number}",
                Info =
                [
                    ("المركز / العيادة", invoice.Clinic?.Name ?? "بدون مركز"),
                    ("الطبيب", invoice.Doctor?.Name ?? "كل الأطباء"),
                    ("من تاريخ", invoice.FromDate.ToString("dd/MM/yyyy")),
                    ("إلى تاريخ", invoice.ToDate.ToString("dd/MM/yyyy")),
                    ("تاريخ الإصدار", invoice.IssueDate.ToString("dd/MM/yyyy")),
                    ("الحالة", invoice.Status)
                ],
                Headers = ["رقم الحالة", "اسم الحالة", "الطبيب", "المنتج", "العدد", "السعر", "الإجمالي"],
                ColumnWidths = [1, 2.2f, 2, 2.4f, 0.8f, 1.2f, 1.4f],
                Rows = invoice.Lines
                    .OrderBy(l => l.DoctorName)
                    .ThenBy(l => l.CaseNumber)
                    .Select(l => new object?[]
                    {
                        l.CaseNumber,
                        l.PatientName,
                        l.DoctorName,
                        l.IsRemake ? $"{l.ProductName} (إعادة)" : l.ProductName,
                        l.Quantity,
                        l.UnitPrice,
                        l.LineTotal
                    }).ToList(),
                Footer = [null, null, null, "الإجمالي", invoice.TotalUnits, null, invoice.TotalAmount],
                Summary =
                [
                    ("إجمالي الفاتورة", invoice.TotalAmount.ToString("N2")),
                    ("المسدد", invoice.PaidAmount.ToString("N2")),
                    ("المتبقي", invoice.Outstanding.ToString("N2"))
                ],
                Note = string.Join("\n", new[]
                {
                    invoice.Notes,
                    string.IsNullOrWhiteSpace(site?.BankAccountNumber)
                        ? null
                        : $"الحساب المصرفي: {site.BankAccountName} — {site.BankName} — {site.BankAccountNumber}"
                }.Where(s => !string.IsNullOrWhiteSpace(s)))
            };

            var (content, type, name) = await export.ExportAsync(report, ExportService.ParseFormat(format));
            return File(content, type, name);
        }

        /// <summary>تسجيل تحصيل على الفاتورة — يُنشئ سطر إيراد مرتبطاً بها.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(Guid id, decimal amount, DateTime? date, Guid? accountId, string? method, string? notes)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            if (invoice.Outstanding <= 0)
            {
                TempData["ErrorMessage"] = "الفاتورة مسددة بالكامل — لا يمكن تسجيل تحصيل إضافي.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "أدخل مبلغاً أكبر من صفر.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (amount > invoice.Outstanding)
            {
                TempData["ErrorMessage"] =
                    $"المبلغ أكبر من المتبقي ({invoice.Outstanding:N2}). لا يمكن تحصيل أكثر من المستحق.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var d = date ?? DateTime.Now.Date;

            _context.IncomeEntries.Add(new IncomeEntry
            {
                Date = d,
                Year = d.Year,
                Month = d.Month,
                Description = $"تحصيل فاتورة رقم {invoice.Number}",
                Amount = amount,
                ClinicId = invoice.ClinicId,
                InvoiceId = invoice.Id,
                FinancialAccountId = accountId,
                Method = method,
                IsReceived = true,
                Notes = notes,
                Created = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await _invoices.RefreshStatusAsync(invoice.Id);

            TempData["SuccessMessage"] = $"تم تسجيل تحصيل {amount:N2}";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            if (invoice.Payments.Count > 0)
            {
                TempData["ErrorMessage"] = "الفاتورة عليها تحصيلات — احذف التحصيلات أولاً من شاشة الإيرادات.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var year = invoice.Year;
            var month = invoice.Month;

            await _invoices.CancelAsync(id);

            TempData["SuccessMessage"] = "أُلغيت الفاتورة وفُكَّ ارتباط حالاتها — يمكنك إصدارها من جديد.";
            return RedirectToAction(nameof(Index), new { year, month });
        }
    }
}
