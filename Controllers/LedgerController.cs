using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Services;
using DentalLab.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// سجل الصندوق: كل الحركات النقدية في الشهر بالترتيب مع رصيد متحرك.
    ///
    /// الداخل  = الإيرادات المستلمة + سداد السلف
    /// الخارج  = المصروفات المدفوعة + السلف الممنوحة
    /// الرصيد  = رصيد افتتاحي + الداخل − الخارج
    ///
    /// المبالغ المفوترة غير المستلمة لا تدخل الصندوق — تُعرض منفصلة كذمم.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin,Employee")]
    public class LedgerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly FinanceService _finance;

        public LedgerController(AppDbContext context, FinanceService finance)
        {
            _context = context;
            _finance = finance;
        }

        public async Task<IActionResult> Index(int? year, int? month)
        {
            var now = DateTime.Now;
            int y = year ?? now.Year;
            int m = month ?? now.Month;

            var vm = new LedgerFilterVM
            {
                Year = y,
                Month = m,
                AvailableYears = await _finance.GetAvailableYearsAsync(),
                OpeningBalance = await GetOpeningBalanceAsync(y, m)
            };

            var income = await _context.IncomeEntries
                .AsNoTracking()
                .Include(e => e.FinancialAccount)
                .Include(e => e.Clinic)
                .Where(e => e.Year == y && e.Month == m)
                .ToListAsync();

            var expenses = await _context.Expenses
                .AsNoTracking()
                .Include(e => e.FinancialAccount)
                .Include(e => e.ExpenseCategory)
                .Where(e => e.Year == y && e.Month == m)
                .ToListAsync();

            var loans = await _context.LoanTransactions
                .AsNoTracking()
                .Include(l => l.FinancialAccount)
                .Where(l => l.Year == y && l.Month == m)
                .ToListAsync();

            var rows = new List<LedgerRow>();

            rows.AddRange(income.Select(e => new LedgerRow
            {
                Date = e.Date,
                Kind = LedgerKind.Income,
                Description = e.Clinic != null ? $"{e.Description} — {e.Clinic.Name}" : e.Description,
                Account = e.FinancialAccount?.Name,
                In = e.IsReceived ? e.Amount : 0m,
                IsPending = !e.IsReceived
            }));

            rows.AddRange(expenses.Select(e => new LedgerRow
            {
                Date = e.Date,
                Kind = LedgerKind.Expense,
                Description = e.ExpenseCategory != null ? $"{e.Description} ({e.ExpenseCategory.Name})" : e.Description,
                Account = e.FinancialAccount?.Name,
                Out = e.IsPaid ? e.Amount : 0m,
                IsPending = !e.IsPaid
            }));

            rows.AddRange(loans.Select(l => new LedgerRow
            {
                Date = l.Date,
                Kind = LedgerKind.ForLoan(l.Direction),
                Description = $"{l.PersonName}{(string.IsNullOrWhiteSpace(l.Description) ? "" : " — " + l.Description)}",
                Account = l.FinancialAccount?.Name,
                In = LoanDirection.IsCashIn(l.Direction) ? l.Amount : 0m,
                Out = LoanDirection.IsCashIn(l.Direction) ? 0m : l.Amount
            }));

            var ordered = rows
                .OrderBy(r => r.Date ?? new DateTime(y, m, 28))
                .ThenBy(r => r.Kind)
                .ToList();

            var running = vm.OpeningBalance;
            foreach (var row in ordered)
            {
                running += row.In - row.Out;
                row.Balance = running;
            }

            vm.Rows = ordered;
            vm.PendingIncome = income.Where(e => !e.IsReceived).Sum(e => e.Amount);

            return View(vm);
        }

        /// <summary>رصيد بداية الشهر = أرصدة الحسابات الافتتاحية + كل حركة نقدية سابقة.</summary>
        private async Task<decimal> GetOpeningBalanceAsync(int year, int month)
        {
            bool Before(int y, int m) => y < year || (y == year && m < month);

            var accountsOpening = await _context.FinancialAccounts
                .AsNoTracking()
                .SumAsync(a => (decimal?)a.OpeningBalance) ?? 0m;

            var priorIncome = await _context.IncomeEntries
                .AsNoTracking()
                .Where(e => e.IsReceived)
                .Select(e => new { e.Year, e.Month, e.Amount })
                .ToListAsync();

            var priorExpenses = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.IsPaid)
                .Select(e => new { e.Year, e.Month, e.Amount })
                .ToListAsync();

            var priorLoans = await _context.LoanTransactions
                .AsNoTracking()
                .Select(l => new { l.Year, l.Month, l.Amount, l.Direction })
                .ToListAsync();

            var inflow = priorIncome.Where(e => Before(e.Year, e.Month)).Sum(e => e.Amount)
                         + priorLoans.Where(l => Before(l.Year, l.Month) && LoanDirection.IsCashIn(l.Direction)).Sum(l => l.Amount);

            var outflow = priorExpenses.Where(e => Before(e.Year, e.Month)).Sum(e => e.Amount)
                          + priorLoans.Where(l => Before(l.Year, l.Month) && !LoanDirection.IsCashIn(l.Direction)).Sum(l => l.Amount);

            return accountsOpening + inflow - outflow;
        }
    }
}
