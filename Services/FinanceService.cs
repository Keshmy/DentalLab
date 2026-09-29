using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.ViewModels.Finance;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// كل حسابات المال في مكان واحد.
    ///
    /// الإيراد المستحق  = Σ (العدد × السعر) لأسطر حالات الشهر (ما عدا الملغي)
    /// التحصيلات        = Σ الإيرادات المستلمة فعلاً
    /// الربح المحاسبي   = الإيراد المستحق − المصروفات التشغيلية − الديون المعدومة
    /// الربح النقدي     = (التحصيلات + سداد السلف) − (المصروفات المدفوعة + السلف الممنوحة)
    /// ذمة العيادة      = رصيد افتتاحي + إجمالي أعمالها − ما حُصّل منها
    /// رصيد السلفة      = الممنوح − المسدد − المشطوب
    ///
    /// السلف ليست مصروفاً: تنقص الكاش فقط، ولا تنقص الربح إلا إذا شُطبت.
    /// الأجهزة (بند رأسمالي) تخرج من الكاش ولا تُحمَّل على ربح الشهر.
    /// </summary>
    public class FinanceService
    {
        private readonly AppDbContext _context;

        public FinanceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<int>> GetAvailableYearsAsync()
        {
            var caseYears = await _context.LabCases.Select(c => c.Year).Distinct().ToListAsync();
            var expenseYears = await _context.Expenses.Select(e => e.Year).Distinct().ToListAsync();
            var incomeYears = await _context.IncomeEntries.Select(e => e.Year).Distinct().ToListAsync();

            var years = caseYears.Concat(expenseYears).Concat(incomeYears)
                .Where(y => y > 2000)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            if (years.Count == 0)
                years.Add(DateTime.Now.Year);

            return years;
        }

        public async Task<FinanceSummaryVM> BuildSummaryAsync(int year, int? month = null)
        {
            var site = await _context.SiteInfo.AsNoTracking().FirstOrDefaultAsync();
            var sharePercent = site?.PartnerSharePercent ?? 0m;

            var vm = new FinanceSummaryVM
            {
                Year = year,
                Month = month,
                AvailableYears = await GetAvailableYearsAsync(),
                PartnerSharePercent = sharePercent,
                PartnerName = site?.PartnerName
            };

            // ===== الإيراد المستحق ووحداته =====
            var revenue = await _context.LabCaseItems
                .Where(i => i.LabCase!.Year == year && i.LabCase.Status != CaseStatus.Cancelled)
                .GroupBy(i => i.LabCase!.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Total = g.Sum(x => x.Quantity * x.UnitPrice),
                    Units = g.Sum(x => x.Quantity),
                    Cost = g.Sum(x => x.Quantity * x.UnitCost)
                })
                .ToListAsync();

            // ===== الإعادات =====
            var remakes = await _context.LabCaseItems
                .Where(i => i.IsRemake && i.LabCase!.Year == year && i.LabCase.Status != CaseStatus.Cancelled)
                .GroupBy(i => i.LabCase!.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Units = g.Sum(x => x.Quantity),
                    Charged = g.Sum(x => x.Quantity * x.UnitPrice),
                    // قيمة الإعادات المجانية بالسعر العام لنوع العمل
                    FreeLoss = g.Where(x => x.UnitPrice == 0)
                                .Sum(x => x.Quantity * (x.ServiceType == null ? 0m : x.ServiceType.UnitPrice)),
                    // تكلفة المواد التي أُهدرت فعلاً في الإعادات المجانية
                    FreeCost = g.Where(x => x.UnitPrice == 0).Sum(x => x.Quantity * x.UnitCost)
                })
                .ToListAsync();

            // ===== المصروفات =====
            var expenses = await _context.Expenses
                .Where(e => e.Year == year)
                .GroupBy(e => e.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    // نسبة الشريك تُحسب من الربح، فلا تُحمَّل هنا مرة ثانية
                    Operating = g.Where(x => (x.ExpenseCategory == null || !x.ExpenseCategory.IsCapital)
                                             && (x.ExpenseCategory == null
                                                 || x.ExpenseCategory.Name != ExpenseCategoryNames.PartnerShare))
                                 .Sum(x => x.Amount),
                    Capital = g.Where(x => x.ExpenseCategory != null && x.ExpenseCategory.IsCapital).Sum(x => x.Amount),
                    PartnerPaid = g.Where(x => x.ExpenseCategory != null
                                               && x.ExpenseCategory.Name == ExpenseCategoryNames.PartnerShare)
                                   .Sum(x => x.Amount),
                    Paid = g.Where(x => x.IsPaid).Sum(x => x.Amount)
                })
                .ToListAsync();

            // ===== الإيرادات المستلمة وغير المستلمة =====
            var income = await _context.IncomeEntries
                .Where(e => e.Year == year)
                .GroupBy(e => e.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Received = g.Where(x => x.IsReceived).Sum(x => x.Amount),
                    Pending = g.Where(x => !x.IsReceived).Sum(x => x.Amount)
                })
                .ToListAsync();

            // ===== السلف =====
            var loans = await _context.LoanTransactions
                .Where(l => l.Year == year)
                .GroupBy(l => l.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Given = g.Where(x => x.Direction == LoanDirection.Given).Sum(x => x.Amount),
                    Repaid = g.Where(x => x.Direction == LoanDirection.Repaid).Sum(x => x.Amount),
                    Borrowed = g.Where(x => x.Direction == LoanDirection.Borrowed).Sum(x => x.Amount),
                    Settled = g.Where(x => x.Direction == LoanDirection.Settled).Sum(x => x.Amount),
                    WrittenOff = g.Where(x => x.IsWrittenOff).Sum(x => x.Amount)
                })
                .ToListAsync();

            var months = month.HasValue
                ? new[] { month.Value }
                : Enumerable.Range(1, 12).ToArray();

            foreach (var m in months)
            {
                var r = revenue.FirstOrDefault(x => x.Month == m);
                var rm = remakes.FirstOrDefault(x => x.Month == m);
                var ex = expenses.FirstOrDefault(x => x.Month == m);
                var inc = income.FirstOrDefault(x => x.Month == m);
                var ln = loans.FirstOrDefault(x => x.Month == m);

                var row = new MonthlyFinanceRow
                {
                    Year = year,
                    Month = m,
                    Revenue = r?.Total ?? 0m,
                    Units = r?.Units ?? 0,
                    MaterialCost = r?.Cost ?? 0m,
                    PartnerSharePercent = sharePercent,
                    PartnerShareRecorded = ex?.PartnerPaid ?? 0m,
                    RemakeUnits = rm?.Units ?? 0,
                    RemakeCharged = rm?.Charged ?? 0m,
                    FreeRemakeLoss = rm?.FreeLoss ?? 0m,
                    FreeRemakeCost = rm?.FreeCost ?? 0m,
                    // نسبة الشريك تُستثنى من المصروفات فقط إذا كانت تُحسب من الربح (النسبة > 0).
                    // أما إذا كانت 0 فالنسبة تُدخل يدوياً كمصروف عادي، فتُحمَّل هنا وإلا اختفت من الربح.
                    OperatingExpenses = (ex?.Operating ?? 0m)
                                        + (sharePercent > 0 ? 0m : ex?.PartnerPaid ?? 0m),
                    CapitalExpenses = ex?.Capital ?? 0m,
                    PaidExpenses = ex?.Paid ?? 0m,
                    Collections = inc?.Received ?? 0m,
                    PendingIncome = inc?.Pending ?? 0m,
                    LoansGiven = ln?.Given ?? 0m,
                    LoansRepaid = ln?.Repaid ?? 0m,
                    LoansBorrowed = ln?.Borrowed ?? 0m,
                    LoansSettled = ln?.Settled ?? 0m,
                    WrittenOffLoans = ln?.WrittenOff ?? 0m
                };

                // لا نُظهر أشهراً فارغة تماماً عند عرض السنة كاملة
                var isEmpty = row.Revenue == 0 && row.OperatingExpenses == 0 && row.CapitalExpenses == 0
                              && row.Collections == 0 && row.PendingIncome == 0
                              && row.LoansGiven == 0 && row.LoansRepaid == 0
                              && row.LoansBorrowed == 0 && row.LoansSettled == 0;

                if (!isEmpty || month.HasValue)
                    vm.Months.Add(row);
            }

            var receivables = await GetClinicReceivablesAsync();
            vm.TotalReceivables = receivables.Sum(x => x.Balance);

            var loanBalances = await GetLoanBalancesAsync();
            vm.TotalLoanBalance = loanBalances.Sum(x => x.Balance);

            return vm;
        }

        /// <summary>ذمم العيادات: رصيد افتتاحي + إجمالي الأعمال − المحصّل.</summary>
        public async Task<List<ClinicReceivableVM>> GetClinicReceivablesAsync()
        {
            var clinics = await _context.Clinics
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            var work = await _context.LabCases
                .Where(c => c.Status != CaseStatus.Cancelled)
                .GroupBy(c => c.ClinicId)
                .Select(g => new
                {
                    ClinicId = g.Key,
                    Total = g.SelectMany(c => c.Items).Sum(i => i.Quantity * i.UnitPrice),
                    Units = g.SelectMany(c => c.Items).Sum(i => i.Quantity),
                    Cases = g.Count()
                })
                .ToListAsync();

            var collected = await _context.IncomeEntries
                .Where(e => e.IsReceived)
                .GroupBy(e => e.ClinicId)
                .Select(g => new { ClinicId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var list = clinics.Select(c => new ClinicReceivableVM
            {
                ClinicId = c.Id,
                ClinicName = c.Name,
                OpeningBalance = c.OpeningBalance,
                Invoiced = work.FirstOrDefault(w => w.ClinicId == c.Id)?.Total ?? 0m,
                Units = work.FirstOrDefault(w => w.ClinicId == c.Id)?.Units ?? 0,
                CaseCount = work.FirstOrDefault(w => w.ClinicId == c.Id)?.Cases ?? 0,
                Collected = collected.FirstOrDefault(p => p.ClinicId == c.Id)?.Total ?? 0m
            }).ToList();

            // أعمال وتحصيلات غير مرتبطة بعيادة
            var orphanWork = work.FirstOrDefault(w => w.ClinicId == null);
            var orphanCollected = collected.FirstOrDefault(p => p.ClinicId == null);

            if (orphanWork != null || orphanCollected != null)
            {
                list.Add(new ClinicReceivableVM
                {
                    ClinicId = null,
                    ClinicName = "بدون عيادة",
                    Invoiced = orphanWork?.Total ?? 0m,
                    Units = orphanWork?.Units ?? 0,
                    CaseCount = orphanWork?.Cases ?? 0,
                    Collected = orphanCollected?.Total ?? 0m
                });
            }

            return list.Where(x => x.Invoiced != 0 || x.Collected != 0 || x.OpeningBalance != 0)
                       .OrderByDescending(x => x.Balance)
                       .ToList();
        }

        /// <summary>
        /// الرصيد الصافي لكل شخص: (سلف منحناها − سُددت − مشطوبة) − (ديون علينا − سددناها).
        /// موجب = مدين لنا، سالب = نحن مدينون له.
        /// </summary>
        public async Task<List<LoanBalanceVM>> GetLoanBalancesAsync()
        {
            var rows = await _context.LoanTransactions
                .AsNoTracking()
                .GroupBy(l => l.PersonName)
                .Select(g => new LoanBalanceVM
                {
                    PersonName = g.Key,
                    Given = g.Where(x => x.Direction == LoanDirection.Given && !x.IsWrittenOff).Sum(x => x.Amount),
                    Repaid = g.Where(x => x.Direction == LoanDirection.Repaid).Sum(x => x.Amount),
                    Borrowed = g.Where(x => x.Direction == LoanDirection.Borrowed).Sum(x => x.Amount),
                    Settled = g.Where(x => x.Direction == LoanDirection.Settled).Sum(x => x.Amount),
                    WrittenOff = g.Where(x => x.IsWrittenOff).Sum(x => x.Amount)
                })
                .ToListAsync();

            return rows.Where(r => r.Balance != 0 || r.Given != 0 || r.Borrowed != 0)
                       .OrderByDescending(r => Math.Abs(r.Balance))
                       .ToList();
        }

        /// <summary>رصيد كل حساب = افتتاحي + الداخل − الخارج.</summary>
        public async Task<List<AccountBalanceVM>> GetAccountBalancesAsync()
        {
            var accounts = await _context.FinancialAccounts
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var incomeIn = await _context.IncomeEntries
                .Where(e => e.IsReceived && e.FinancialAccountId != null)
                .GroupBy(e => e.FinancialAccountId!.Value)
                .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var loanIn = await _context.LoanTransactions
                .Where(l => (l.Direction == LoanDirection.Repaid || l.Direction == LoanDirection.Borrowed)
                            && l.FinancialAccountId != null)
                .GroupBy(l => l.FinancialAccountId!.Value)
                .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var expenseOut = await _context.Expenses
                .Where(e => e.IsPaid && e.FinancialAccountId != null)
                .GroupBy(e => e.FinancialAccountId!.Value)
                .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var loanOut = await _context.LoanTransactions
                .Where(l => (l.Direction == LoanDirection.Given || l.Direction == LoanDirection.Settled)
                            && l.FinancialAccountId != null)
                .GroupBy(l => l.FinancialAccountId!.Value)
                .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            return accounts.Select(a => new AccountBalanceVM
            {
                AccountId = a.Id,
                Name = a.Name,
                Kind = a.Kind,
                OpeningBalance = a.OpeningBalance,
                In = (incomeIn.FirstOrDefault(x => x.Id == a.Id)?.Total ?? 0m)
                     + (loanIn.FirstOrDefault(x => x.Id == a.Id)?.Total ?? 0m),
                Out = (expenseOut.FirstOrDefault(x => x.Id == a.Id)?.Total ?? 0m)
                      + (loanOut.FirstOrDefault(x => x.Id == a.Id)?.Total ?? 0m)
            }).ToList();
        }
    }
}
