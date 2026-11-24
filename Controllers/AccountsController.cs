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

            ViewBag.Transfers = await _context.AccountTransfers
                .AsNoTracking()
                .Include(t => t.FromAccount)
                .Include(t => t.ToAccount)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.Created)
                .Take(50)
                .ToListAsync();

            return View(balances);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new FinancialAccount
            {
                Kind = AccountKind.Cash,
                IsActive = true
            };

            FillLookups(model);
            await FillAccountsAsync(null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FinancialAccount model, Guid? fundFromAccountId, decimal? fundAmount)
        {
            // تمويل اختياري للحساب الجديد بتحويل من حساب موجود (بدل تعديل الأرصدة يدوياً)
            var funding = fundFromAccountId.HasValue && fundAmount is > 0;
            if (fundFromAccountId.HasValue && !(fundAmount is > 0))
                ModelState.AddModelError("fundAmount", "أدخل مبلغ التحويل أو اترك الحساب المحوَّل منه فارغاً");
            if (funding)
                await ValidateTransferSourceAsync(fundFromAccountId!.Value, fundAmount!.Value, "fundAmount");

            if (!ModelState.IsValid)
            {
                FillLookups(model);
                await FillAccountsAsync(fundFromAccountId);
                ViewBag.FundAmount = fundAmount;
                return View(model);
            }

            model.Created = DateTime.UtcNow;
            _context.FinancialAccounts.Add(model);

            if (funding)
            {
                var today = DateTime.Today;
                _context.AccountTransfers.Add(new AccountTransfer
                {
                    FromAccountId = fundFromAccountId!.Value,
                    ToAccount = model,
                    Amount = fundAmount!.Value,
                    Date = today,
                    Year = today.Year,
                    Month = today.Month,
                    Notes = "تمويل الحساب عند إنشائه",
                    Created = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = funding
                ? $"تم إضافة الحساب وتحويل {fundAmount!.Value:N2} إليه"
                : "تم إضافة الحساب";
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
            var transfers = await _context.AccountTransfers.CountAsync(t => t.FromAccountId == id || t.ToAccountId == id);

            if (expenses + income + loans + transfers > 0)
            {
                TempData["ErrorMessage"] =
                    $"لا يمكن حذف الحساب «{item.Name}» لأنه مرتبط بحركات مالية "
                    + $"({expenses} مصروف، {income} إيراد، {loans} سلفة، {transfers} تحويل). "
                    + "حذفه يفقد هذه الحركات نسبتها إلى صندوقها ويُفسد أرصدة الصندوق. "
                    + "عطِّل الحساب بإلغاء علامة «نشط» بدل حذفه.";

                return RedirectToAction(nameof(Index));
            }

            _context.FinancialAccounts.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الحساب";
            return RedirectToAction(nameof(Index));
        }

        // ===== التحويل بين الحسابات =====

        [HttpGet]
        public async Task<IActionResult> Transfer(Guid? fromId)
        {
            var model = new AccountTransfer { Date = DateTime.Today };
            if (fromId.HasValue)
                model.FromAccountId = fromId.Value;

            await FillTransferLookupsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(AccountTransfer model)
        {
            if (model.FromAccountId == Guid.Empty)
                ModelState.AddModelError(nameof(model.FromAccountId), "اختر الحساب المحوَّل منه");
            if (model.ToAccountId == Guid.Empty)
                ModelState.AddModelError(nameof(model.ToAccountId), "اختر الحساب المحوَّل إليه");
            if (model.FromAccountId != Guid.Empty && model.FromAccountId == model.ToAccountId)
                ModelState.AddModelError(nameof(model.ToAccountId), "لا يمكن التحويل إلى نفس الحساب");

            if (model.ToAccountId != Guid.Empty
                && !await _context.FinancialAccounts.AnyAsync(a => a.Id == model.ToAccountId && a.IsActive))
                ModelState.AddModelError(nameof(model.ToAccountId), "الحساب المحوَّل إليه غير موجود أو معطّل");

            if (model.FromAccountId != Guid.Empty && model.Amount > 0)
                await ValidateTransferSourceAsync(model.FromAccountId, model.Amount, nameof(model.Amount));

            if (!ModelState.IsValid)
            {
                await FillTransferLookupsAsync(model);
                return View(model);
            }

            model.Id = Guid.Empty;
            model.Year = model.Date.Year;
            model.Month = model.Date.Month;
            model.Created = DateTime.UtcNow;
            _context.AccountTransfers.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم تحويل {model.Amount:N2}";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>إلغاء تحويل خاطئ: يعود المبلغ إلى الحساب المحوَّل منه.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransfer(Guid id)
        {
            var item = await _context.AccountTransfers.FirstOrDefaultAsync(t => t.Id == id);
            if (item == null)
                return View("NotFound");

            // إلغاء التحويل يُنقص رصيد الحساب المحوَّل إليه — لا نسمح أن يصير سالباً
            var balances = await _finance.GetAccountBalancesAsync();
            var to = balances.FirstOrDefault(b => b.AccountId == item.ToAccountId);
            if (to != null && to.Balance - item.Amount < 0)
            {
                TempData["ErrorMessage"] =
                    $"لا يمكن إلغاء التحويل: رصيد «{to.Name}» الحالي {to.Balance:N2} أقل من مبلغ التحويل {item.Amount:N2} "
                    + "(صُرف جزء منه). حوّل المبلغ المتبقي يدوياً بدلاً من الإلغاء.";
                return RedirectToAction(nameof(Index));
            }

            _context.AccountTransfers.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إلغاء التحويل";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>الحساب المحوَّل منه يجب أن يكون نشطاً وفيه رصيد يكفي.</summary>
        private async Task ValidateTransferSourceAsync(Guid fromId, decimal amount, string field)
        {
            var from = await _context.FinancialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == fromId);
            if (from == null || !from.IsActive)
            {
                ModelState.AddModelError(field, "الحساب المحوَّل منه غير موجود أو معطّل");
                return;
            }

            var balance = (await _finance.GetAccountBalancesAsync())
                .FirstOrDefault(b => b.AccountId == fromId)?.Balance ?? from.OpeningBalance;
            if (amount > balance)
                ModelState.AddModelError(field, $"المبلغ أكبر من رصيد «{from.Name}» ({balance:N2})");
        }

        private async Task FillTransferLookupsAsync(AccountTransfer model)
        {
            await FillAccountsAsync(null);
            var items = (SelectList)ViewBag.FundAccounts;
            ViewBag.FromAccounts = new SelectList(items.Items, "Id", "Text", model.FromAccountId == Guid.Empty ? null : model.FromAccountId);
            ViewBag.ToAccounts = new SelectList(items.Items, "Id", "Text", model.ToAccountId == Guid.Empty ? null : model.ToAccountId);
        }

        /// <summary>الحسابات النشطة مع رصيد كل منها، لقوائم الاختيار.</summary>
        private async Task FillAccountsAsync(Guid? selected)
        {
            var balances = await _finance.GetAccountBalancesAsync();
            var activeIds = await _context.FinancialAccounts.AsNoTracking()
                .Where(a => a.IsActive).Select(a => a.Id).ToListAsync();

            var items = balances
                .Where(b => !b.IsUnassigned && activeIds.Contains(b.AccountId))
                .Select(b => new { Id = b.AccountId, Text = $"{b.Name} — رصيد {b.Balance:N2}" })
                .ToList();
            ViewBag.FundAccounts = new SelectList(items, "Id", "Text", selected);
        }

        private void FillLookups(FinancialAccount model)
        {
            ViewBag.Kinds = new SelectList(AccountKind.All, model.Kind);
        }
    }
}
