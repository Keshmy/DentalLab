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
                TempData["ErrorMessage"] = "لا توجد أعمال في هذه الفترة — لم تُصدر أي فاتورة.";
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

        /// <summary>تسجيل تحصيل على الفاتورة — يُنشئ سطر إيراد مرتبطاً بها.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(Guid id, decimal amount, DateTime? date, Guid? accountId, string? method, string? notes)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null)
                return View("NotFound");

            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "أدخل مبلغاً أكبر من صفر.";
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
