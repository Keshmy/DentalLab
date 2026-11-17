using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// الإيرادات والتحصيلات.
    ///
    /// المستلم فعلاً هو ما يدخل الصندوق، وغير المستلم يبقى ديناً على المركز.
    /// ربط الإيراد بالفاتورة يتم من شاشة الفواتير، فلا يُدخل هنا يدوياً.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class IncomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly FinanceService _finance;
        private readonly InvoiceService _invoices;

        public IncomeController(AppDbContext context, FinanceService finance, InvoiceService invoices)
        {
            _context = context;
            _finance = finance;
            _invoices = invoices;
        }

        public async Task<IActionResult> Index(int? year, int? month, Guid? clinicId, bool? pendingOnly)
        {
            var list = await FilterAsync(year, month, clinicId, pendingOnly);
            await FillFilters(year, month, clinicId, pendingOnly);
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Export(int? year, int? month, Guid? clinicId, bool? pendingOnly, string? format,
            [FromServices] ExportService export)
        {
            var list = await FilterAsync(year, month, clinicId, pendingOnly);
            var received = list.Where(e => e.IsReceived).Sum(e => e.Amount);

            var report = new ReportTable
            {
                Title = "الإيرادات والتحصيلات",
                Subtitle = year.HasValue ? (month.HasValue ? $"شهر {month}/{year}" : $"سنة {year}") : "كل الفترات",
                FileName = $"Income-{DateTime.Now:yyyy-MM-dd}",
                Headers = ["التاريخ", "البيان", "المركز", "الفاتورة", "الحساب", "الطريقة", "المبلغ", "مستلم"],
                ColumnWidths = [1.1f, 3, 2, 0.9f, 1.3f, 1, 1.2f, 0.8f],
                Rows = list.Select(e => new object?[]
                {
                    e.Date, e.Description, e.Clinic?.Name, e.Invoice?.Number, e.FinancialAccount?.Name, e.Method, e.Amount,
                    e.IsReceived ? "نعم" : "لا"
                }).ToList(),
                Footer = [null, "الإجمالي", null, null, null, null, list.Sum(e => e.Amount), null],
                Summary =
                [
                    ("المستلم", received.ToString("N2")),
                    ("لم يُستلم", (list.Sum(e => e.Amount) - received).ToString("N2"))
                ]
            };

            var (content, type, name) = await export.ExportAsync(report, ExportService.ParseFormat(format));
            return File(content, type, name);
        }

        private async Task<List<IncomeEntry>> FilterAsync(int? year, int? month, Guid? clinicId, bool? pendingOnly)
        {
            var query = _context.IncomeEntries
                .AsNoTracking()
                .Include(e => e.Clinic)
                .Include(e => e.FinancialAccount)
                .Include(e => e.Invoice)
                .AsQueryable();

            if (year.HasValue)
                query = query.Where(e => e.Year == year.Value);
            if (month.HasValue)
                query = query.Where(e => e.Month == month.Value);
            if (clinicId.HasValue)
                query = query.Where(e => e.ClinicId == clinicId.Value);
            if (pendingOnly == true)
                query = query.Where(e => !e.IsReceived);

            return await query
                .OrderByDescending(e => e.Year)
                .ThenByDescending(e => e.Month)
                .ThenByDescending(e => e.Date)
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var now = DateTime.Now;
            var model = new IncomeEntry
            {
                Date = now.Date,
                Year = now.Year,
                Month = now.Month,
                Method = PaymentMethod.Cash,
                IsReceived = true
            };

            await FillLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IncomeEntry model)
        {
            ApplyPeriod(model);

            if (!ModelState.IsValid)
            {
                await FillLookups(model);
                return View(model);
            }

            // ربط الفاتورة يتم من شاشة الفواتير وليس من هنا
            model.InvoiceId = null;
            model.Created = DateTime.UtcNow;
            _context.IncomeEntries.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الإيراد";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var item = await _context.IncomeEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (item == null)
                return View("NotFound");

            await FillLookups(item);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, IncomeEntry model)
        {
            if (id != model.Id)
                return View("NotFound");

            var item = await _context.IncomeEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (item == null)
                return View("NotFound");

            ApplyPeriod(model);

            if (!ModelState.IsValid)
            {
                await FillLookups(model);
                return View(model);
            }

            // تحصيل مرتبط بفاتورة: نفس العيادة، ولا يتجاوز مجموع التحصيلات قيمة الفاتورة.
            if (item.InvoiceId.HasValue)
            {
                var error = await InvoicePaymentErrorAsync(item.InvoiceId.Value, item.Id, model.Amount, model.IsReceived, model.ClinicId);
                if (error != null)
                {
                    ModelState.AddModelError(string.Empty, error);
                    await FillLookups(model);
                    return View(model);
                }
            }

            // لا نلمس InvoiceId: ارتباط الإيراد بالفاتورة تديره شاشة الفواتير
            item.Date = model.Date;
            item.Year = model.Year;
            item.Month = model.Month;
            item.Description = model.Description;
            item.Amount = model.Amount;
            item.ClinicId = model.ClinicId;
            item.FinancialAccountId = model.FinancialAccountId;
            item.Method = model.Method;
            item.IsReceived = model.IsReceived;
            item.Notes = model.Notes;
            item.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            if (item.InvoiceId.HasValue)
                await _invoices.RefreshStatusAsync(item.InvoiceId.Value);

            TempData["SuccessMessage"] = "تم حفظ الإيراد";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>تأكيد استلام المبلغ: يتحول من دين على المركز إلى نقد في الصندوق.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReceived(Guid id, int? year, int? month)
        {
            var item = await _context.IncomeEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (item == null)
                return View("NotFound");

            if (item.InvoiceId.HasValue)
            {
                var error = await InvoicePaymentErrorAsync(item.InvoiceId.Value, item.Id, item.Amount, true, item.ClinicId);
                if (error != null)
                {
                    TempData["ErrorMessage"] = error;
                    return RedirectToAction(nameof(Index), new { year, month });
                }
            }

            item.IsReceived = true;
            item.Date ??= DateTime.Now.Date;
            item.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            if (item.InvoiceId.HasValue)
                await _invoices.RefreshStatusAsync(item.InvoiceId.Value);

            TempData["SuccessMessage"] = "تم تأكيد استلام المبلغ";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.IncomeEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (item == null)
                return View("NotFound");

            var invoiceId = item.InvoiceId;
            _context.IncomeEntries.Remove(item);
            await _context.SaveChangesAsync();
            // حذف تحصيل فاتورة يعيد حالتها (مسددة ← مسددة جزئياً / صادرة)
            if (invoiceId.HasValue)
                await _invoices.RefreshStatusAsync(invoiceId.Value);

            TempData["SuccessMessage"] = "تم حذف الإيراد";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// قواعد تحصيل الفاتورة نفسها التي تطبقها شاشة الفواتير: العيادة لا تتغير، ومجموع المستلم لا يتجاوز قيمة الفاتورة.
        /// </summary>
        private async Task<string?> InvoicePaymentErrorAsync(Guid invoiceId, Guid paymentId, decimal amount, bool isReceived, Guid? clinicId)
        {
            var invoice = await _context.Invoices.AsNoTracking()
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                return null;

            if (clinicId != invoice.ClinicId)
                return $"هذا التحصيل مرتبط بالفاتورة رقم {invoice.Number}، فلا يمكن نقله إلى عيادة أخرى. " +
                       "إن كان مسجلاً خطأً فاحذفه وسجّل التحصيل الصحيح.";

            if (!isReceived)
                return null;

            var otherPaid = invoice.Payments.Where(p => p.Id != paymentId && p.IsReceived).Sum(p => p.Amount);
            var remaining = invoice.TotalAmount - otherPaid;
            if (amount > remaining)
                return $"المبلغ أكبر من المتبقي على الفاتورة رقم {invoice.Number} ({remaining:N2}). لا يمكن تحصيل أكثر من المستحق.";

            return null;
        }

        /// <summary>السنة والشهر أساس كل التقارير، فنشتقهما من التاريخ إن تُركا فارغين.</summary>
        private static void ApplyPeriod(IncomeEntry model)
        {
            var d = model.Date ?? DateTime.Now;
            if (model.Year == 0)
                model.Year = d.Year;
            if (model.Month == 0)
                model.Month = d.Month;
        }

        private async Task FillLookups(IncomeEntry model)
        {
            ViewBag.Clinics = new SelectList(
                await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", model.ClinicId);

            ViewBag.Accounts = new SelectList(
                await _context.FinancialAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                "Id", "Name", model.FinancialAccountId);

            ViewBag.Methods = new SelectList(PaymentMethod.All, model.Method);
        }

        private async Task FillFilters(int? year, int? month, Guid? clinicId, bool? pendingOnly)
        {
            ViewBag.Years = new SelectList(await _finance.GetAvailableYearsAsync(), year);
            ViewBag.Clinics = new SelectList(
                await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", clinicId);

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.ClinicId = clinicId;
            ViewBag.PendingOnly = pendingOnly;
        }
    }
}
