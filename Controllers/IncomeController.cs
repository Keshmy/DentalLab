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

        public IncomeController(AppDbContext context, FinanceService finance)
        {
            _context = context;
            _finance = finance;
        }

        public async Task<IActionResult> Index(int? year, int? month, Guid? clinicId, bool? pendingOnly)
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

            var list = await query
                .OrderByDescending(e => e.Year)
                .ThenByDescending(e => e.Month)
                .ThenByDescending(e => e.Date)
                .ToListAsync();

            await FillFilters(year, month, clinicId, pendingOnly);
            return View(list);
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

            item.IsReceived = true;
            item.Date ??= DateTime.Now.Date;
            item.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

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

            _context.IncomeEntries.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الإيراد";
            return RedirectToAction(nameof(Index));
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
