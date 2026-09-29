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
    /// الحسابات والصناديق: رصيد كل حساب = الرصيد الافتتاحي + الداخل − الخارج.
    ///
    /// الداخل  = الإيرادات المستلمة + سداد السلف
    /// الخارج  = المصروفات المدفوعة + السلف الممنوحة
    ///
    /// لا يُحذف حساب مرتبط بحركات، لأن حذفه يفقد الحركات نسبتها إلى صندوقها.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class AccountsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly FinanceService _finance;

        public AccountsController(AppDbContext context, FinanceService finance)
        {
            _context = context;
            _finance = finance;
        }

        public async Task<IActionResult> Index()
        {
            // أرصدة الحسابات النشطة فقط — والحسابات المعطّلة تُعرض منفصلة لتبقى قابلة للتعديل
            var balances = await _finance.GetAccountBalancesAsync();

            ViewBag.InactiveAccounts = await _context.FinancialAccounts
                .AsNoTracking()
                .Where(a => !a.IsActive)
                .OrderBy(a => a.Name)
                .ToListAsync();

            return View(balances);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new FinancialAccount
            {
                Kind = AccountKind.Cash,
                IsActive = true
            };

            FillLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FinancialAccount model)
        {
            if (!ModelState.IsValid)
            {
                FillLookups(model);
                return View(model);
            }

            model.Created = DateTime.UtcNow;
            _context.FinancialAccounts.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الحساب";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var item = await _context.FinancialAccounts.FirstOrDefaultAsync(a => a.Id == id);
            if (item == null)
                return View("NotFound");

            FillLookups(item);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, FinancialAccount model)
        {
            if (id != model.Id)
                return View("NotFound");

            var item = await _context.FinancialAccounts.FirstOrDefaultAsync(a => a.Id == id);
            if (item == null)
                return View("NotFound");

            if (!ModelState.IsValid)
            {
                FillLookups(model);
                return View(model);
            }

            item.Name = model.Name;
            item.Kind = model.Kind;
            item.OpeningBalance = model.OpeningBalance;
            item.IsActive = model.IsActive;
            item.Notes = model.Notes;
            item.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حفظ الحساب";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.FinancialAccounts.FirstOrDefaultAsync(a => a.Id == id);
            if (item == null)
                return View("NotFound");

            var expenses = await _context.Expenses.CountAsync(e => e.FinancialAccountId == id);
            var income = await _context.IncomeEntries.CountAsync(e => e.FinancialAccountId == id);
            var loans = await _context.LoanTransactions.CountAsync(l => l.FinancialAccountId == id);

            if (expenses + income + loans > 0)
            {
                TempData["ErrorMessage"] =
                    $"لا يمكن حذف الحساب «{item.Name}» لأنه مرتبط بحركات مالية "
                    + $"({expenses} مصروف، {income} إيراد، {loans} سلفة). "
                    + "حذفه يفقد هذه الحركات نسبتها إلى صندوقها ويُفسد أرصدة الصندوق. "
                    + "عطِّل الحساب بإلغاء علامة «نشط» بدل حذفه.";

                return RedirectToAction(nameof(Index));
            }

            _context.FinancialAccounts.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الحساب";
            return RedirectToAction(nameof(Index));
        }

        private void FillLookups(FinancialAccount model)
        {
            ViewBag.Kinds = new SelectList(AccountKind.All, model.Kind);
        }
    }
}
