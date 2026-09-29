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
    /// السلف: ليست مصروفاً.
    ///
    /// السلفة الممنوحة تنقص الصندوق وتُسجَّل ذمة على الشخص، ولا تنقص الربح.
    /// السداد يعيد المال إلى الصندوق ويُنقص الذمة.
    /// الشطب (دين معدوم) هو وحده ما يتحول إلى خسارة تُحمَّل على الأرباح.
    ///
    /// كل الأرصدة تُجمَّع باسم الشخص (PersonName)، فلا يُترك فارغاً أبداً.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class LoansController : Controller
    {
        private readonly AppDbContext _context;
        private readonly FinanceService _finance;

        public LoansController(AppDbContext context, FinanceService finance)
        {
            _context = context;
            _finance = finance;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Balances = await _finance.GetLoanBalancesAsync();

            var list = await _context.LoanTransactions
                .AsNoTracking()
                .Include(l => l.FinancialAccount)
                .Include(l => l.Employee)
                .OrderByDescending(l => l.Year)
                .ThenByDescending(l => l.Month)
                .ThenByDescending(l => l.Date)
                .ToListAsync();

            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var now = DateTime.Now;
            var model = new LoanTransaction
            {
                Date = now.Date,
                Year = now.Year,
                Month = now.Month,
                Direction = LoanDirection.Given
            };

            await FillLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LoanTransaction model)
        {
            ApplyPeriod(model);
            await ApplyPersonNameAsync(model);
            ValidatePerson(model);

            if (!ModelState.IsValid)
            {
                await FillLookups(model);
                return View(model);
            }

            model.Created = DateTime.UtcNow;
            _context.LoanTransactions.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة حركة السلفة";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var item = await _context.LoanTransactions.FirstOrDefaultAsync(l => l.Id == id);
            if (item == null)
                return View("NotFound");

            await FillLookups(item);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, LoanTransaction model)
        {
            if (id != model.Id)
                return View("NotFound");

            var item = await _context.LoanTransactions.FirstOrDefaultAsync(l => l.Id == id);
            if (item == null)
                return View("NotFound");

            ApplyPeriod(model);
            await ApplyPersonNameAsync(model);
            ValidatePerson(model);

            if (!ModelState.IsValid)
            {
                await FillLookups(model);
                return View(model);
            }

            item.Date = model.Date;
            item.Year = model.Year;
            item.Month = model.Month;
            item.PersonName = model.PersonName;
            item.EmployeeId = model.EmployeeId;
            item.Amount = model.Amount;
            item.Direction = model.Direction;
            item.FinancialAccountId = model.FinancialAccountId;
            item.Description = model.Description;
            item.IsWrittenOff = model.IsWrittenOff;
            item.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حفظ حركة السلفة";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.LoanTransactions.FirstOrDefaultAsync(l => l.Id == id);
            if (item == null)
                return View("NotFound");

            _context.LoanTransactions.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف حركة السلفة";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>السنة والشهر أساس كل التقارير، فنشتقهما من التاريخ إن تُركا فارغين.</summary>
        private static void ApplyPeriod(LoanTransaction model)
        {
            var d = model.Date ?? DateTime.Now;
            if (model.Year == 0)
                model.Year = d.Year;
            if (model.Month == 0)
                model.Month = d.Month;
        }

        /// <summary>أرصدة السلف تُجمَّع باسم الشخص، فنأخذه من الموظف المختار إن تُرك الاسم فارغاً.</summary>
        private async Task ApplyPersonNameAsync(LoanTransaction model)
        {
            if (!string.IsNullOrWhiteSpace(model.PersonName) || !model.EmployeeId.HasValue)
                return;

            var name = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == model.EmployeeId.Value)
                .Select(e => e.Name)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(name))
                return;

            model.PersonName = name;
            ModelState.Remove(nameof(LoanTransaction.PersonName));
        }

        /// <summary>الاسم مطلوب إن لم يُختر موظف — وإلا يُملأ من اسم الموظف.</summary>
        private void ValidatePerson(LoanTransaction model)
        {
            if (string.IsNullOrWhiteSpace(model.PersonName) && !model.EmployeeId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(LoanTransaction.PersonName),
                    "أدخل اسم الشخص أو اختر موظفاً");
            }
        }

        private async Task FillLookups(LoanTransaction model)
        {
            ViewBag.Employees = new SelectList(
                await _context.Employees.AsNoTracking().OrderBy(e => e.Name).ToListAsync(),
                "Id", "Name", model.EmployeeId);

            ViewBag.Accounts = new SelectList(
                await _context.FinancialAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                "Id", "Name", model.FinancialAccountId);

            ViewBag.Directions = new SelectList(LoanDirection.All, model.Direction);
        }
    }
}
