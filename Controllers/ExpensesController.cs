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
    /// المصاريف: كل سطر مصروف بشهره وبنده وحسابه.
    ///
    /// غير المدفوع يبقى التزاماً ولا يخرج من الصندوق.
    /// الأجهزة (بند رأسمالي) تخرج من الصندوق ولا تُحمَّل كاملة على ربح الشهر.
    /// نسبة الشريك تُحسب آلياً من الربح، فإدخالها هنا كمصروف يُحتسب مرتين.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class ExpensesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly FinanceService _finance;

        public ExpensesController(AppDbContext context, FinanceService finance)
        {
            _context = context;
            _finance = finance;
        }

        public async Task<IActionResult> Index(int? year, int? month, Guid? categoryId, bool? unpaidOnly)
        {
            var query = _context.Expenses
                .AsNoTracking()
                .Include(e => e.ExpenseCategory)
                .Include(e => e.FinancialAccount)
                .AsQueryable();

            if (year.HasValue)
                query = query.Where(e => e.Year == year.Value);
            if (month.HasValue)
                query = query.Where(e => e.Month == month.Value);
            if (categoryId.HasValue)
                query = query.Where(e => e.ExpenseCategoryId == categoryId.Value);
            if (unpaidOnly == true)
                query = query.Where(e => !e.IsPaid);

            var list = await query
                .OrderByDescending(e => e.Year)
                .ThenByDescending(e => e.Month)
                .ThenByDescending(e => e.Date)
                .ToListAsync();

            await FillFilters(year, month, categoryId, unpaidOnly);
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var now = DateTime.Now;
            var model = new Expense
            {
                Date = now.Date,
                Year = now.Year,
                Month = now.Month,
                IsPaid = true
            };

            await FillLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Expense model)
        {
            ApplyPeriod(model);

            if (!ModelState.IsValid)
            {
                await FillLookups(model);
                return View(model);
            }

            model.Created = DateTime.UtcNow;
            _context.Expenses.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة المصروف";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var item = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id);
            if (item == null)
                return View("NotFound");

            await FillLookups(item);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Expense model)
        {
            if (id != model.Id)
                return View("NotFound");

            var item = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id);
            if (item == null)
                return View("NotFound");

            ApplyPeriod(model);

            if (!ModelState.IsValid)
            {
                await FillLookups(model);
                return View(model);
            }

            item.Date = model.Date;
            item.Year = model.Year;
            item.Month = model.Month;
            item.Description = model.Description;
            item.Amount = model.Amount;
            item.ExpenseCategoryId = model.ExpenseCategoryId;
            item.FinancialAccountId = model.FinancialAccountId;
            item.IsPaid = model.IsPaid;
            item.Notes = model.Notes;
            item.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حفظ المصروف";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id);
            if (item == null)
                return View("NotFound");

            _context.Expenses.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف المصروف";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>السنة والشهر أساس كل التقارير، فنشتقهما من التاريخ إن تُركا فارغين.</summary>
        private static void ApplyPeriod(Expense model)
        {
            var d = model.Date ?? DateTime.Now;
            if (model.Year == 0)
                model.Year = d.Year;
            if (model.Month == 0)
                model.Month = d.Month;
        }

        private async Task FillLookups(Expense model)
        {
            ViewBag.Categories = new SelectList(
                await _context.ExpenseCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", model.ExpenseCategoryId);

            ViewBag.Accounts = new SelectList(
                await _context.FinancialAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                "Id", "Name", model.FinancialAccountId);
        }

        private async Task FillFilters(int? year, int? month, Guid? categoryId, bool? unpaidOnly)
        {
            ViewBag.Years = new SelectList(await _finance.GetAvailableYearsAsync(), year);
            ViewBag.Categories = new SelectList(
                await _context.ExpenseCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", categoryId);

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.CategoryId = categoryId;
            ViewBag.UnpaidOnly = unpaidOnly;
        }
    }
}
