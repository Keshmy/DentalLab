using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.ViewModels.Finance;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// مطابقة الحسابات: يقارن كل رقم مالي بالرقم المقابل له من مصدر آخر، ويسرد البنود التي تفسّر أي فرق.
    ///
    /// 1. الصندوق:   مجموع أرصدة الحسابات (شاشة الحسابات)      = رصيد سجل الصندوق حتى اليوم
    /// 2. الذمم:     مجموع ذمم المراكز                          = مجموع أرصدة كشوف الحساب
    /// 3. الإيراد:   إيراد الأرباح والخسائر (كل السنوات)         = إجمالي أعمال المراكز في الذمم
    /// 4. التحصيل:   تحصيلات الأرباح والخسائر (كل السنوات)       = المحصَّل في الذمم
    /// 5. الفواتير:  إجمالي الفاتورة                             = مجموع أسطرها المثبَّتة
    /// 6. الفواتير:  أسطر الفاتورة                               = أسطر حالاتها الحالية
    /// 7. الفواتير:  حالة الفاتورة                               = الحالة المحسوبة من التحصيلات
    /// 8. السلف:     الديون المعدومة في الأرباح                  = الشطب الفعلي في أرصدة السلف
    ///
    /// الأرقام تُحسب من نفس الخدمات التي تعرضها الشاشات (FinanceService / StatementService)
    /// حتى يكشف الفحص أي اختلاف بين الشاشات نفسها. الصفحة للقراءة فقط ولا تعدّل أي سجل.
    /// </summary>
    public class ReconciliationService
    {
        private const int MaxItemsPerCheck = 100;

        private readonly AppDbContext _context;
        private readonly FinanceService _finance;
        private readonly StatementService _statements;

        public ReconciliationService(AppDbContext context, FinanceService finance, StatementService statements)
        {
            _context = context;
            _finance = finance;
            _statements = statements;
        }

        public async Task<ReconciliationVM> BuildAsync()
        {
            var today = DateTime.Today;
            var vm = new ReconciliationVM { AsOf = DateTime.Now };

            var receivables = await _finance.GetClinicReceivablesAsync();

            vm.Checks.Add(await CashCheckAsync(today));
            vm.Checks.Add(await ReceivablesCheckAsync(receivables));
            vm.Checks.AddRange(await ProfitLossChecksAsync(receivables, today));
            vm.Checks.AddRange(await InvoiceChecksAsync(today));
            vm.Checks.Add(await LoanWriteOffCheckAsync(today));

            foreach (var check in vm.Checks)
                check.Items = check.Items
                    .OrderByDescending(i => i.AgeDays ?? -1)
                    .ThenByDescending(i => Math.Abs(i.Amount))
                    .Take(MaxItemsPerCheck)
                    .ToList();

            vm.OpenItems = await OpenItemsAsync(today);
            return vm;
        }

        // ===== 1. الصندوق =====

        private async Task<ReconciliationCheck> CashCheckAsync(DateTime today)
        {
            var accounts = await _finance.GetAccountBalancesAsync();

            // نفس معادلة سجل الصندوق (LedgerController): افتتاحي الحسابات + كل داخل − كل خارج، بلا حد زمني.
            var opening = await _context.FinancialAccounts.SumAsync(a => (decimal?)a.OpeningBalance) ?? 0m;
            var incomeIn = await _context.IncomeEntries.Where(e => e.IsReceived).SumAsync(e => (decimal?)e.Amount) ?? 0m;
            var expenseOut = await _context.Expenses.Where(e => e.IsPaid).SumAsync(e => (decimal?)e.Amount) ?? 0m;
            var loanIn = await _context.LoanTransactions
                .Where(l => l.Direction == LoanDirection.Repaid || l.Direction == LoanDirection.Borrowed)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;
            var loanOut = await _context.LoanTransactions
                .Where(l => l.Direction != LoanDirection.Repaid && l.Direction != LoanDirection.Borrowed)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            var check = new ReconciliationCheck
            {
                Key = "cash",
                Title = "الصندوق ↔ الحسابات",
                Description = "مجموع أرصدة الحسابات يجب أن يساوي رصيد سجل الصندوق. الحركات بدون حساب تظهر في سطر «بدون حساب» وتُدرج هنا لتحديد حسابها.",
                LeftLabel = "مجموع أرصدة الحسابات",
                LeftValue = accounts.Sum(a => a.Balance),
                RightLabel = "رصيد سجل الصندوق",
                RightValue = opening + incomeIn + loanIn - expenseOut - loanOut,
                FixController = "Accounts",
                FixAction = "Index",
                FixLabel = "الحسابات"
            };

            // حسابات موقوفة ما زال فيها مال
            foreach (var a in accounts.Where(a => !a.IsUnassigned && a.Name.EndsWith("(موقوف)") && a.Balance != 0))
            {
                check.Items.Add(new ReconcilingItem
                {
                    Description = $"حساب موقوف ما زال فيه رصيد: {a.Name}",
                    Amount = a.Balance,
                    Category = ReconcilingCategory.Adjustment,
                    LinkController = "Accounts",
                    LinkAction = "Edit",
                    LinkId = a.AccountId
                });
            }

            // حركات نقدية بدون حساب — سطر واحد لكل نوع (غالباً بيانات مستوردة قديمة)، لا سطر لكل حركة.
            var unassigned = new (string Label, string Controller, int Count, decimal Net)[]
            {
                ("إيرادات مستلمة", "Income",
                    await _context.IncomeEntries.CountAsync(e => e.IsReceived && e.FinancialAccountId == null),
                    await _context.IncomeEntries.Where(e => e.IsReceived && e.FinancialAccountId == null).SumAsync(e => (decimal?)e.Amount) ?? 0m),
                ("مصروفات مدفوعة", "Expenses",
                    await _context.Expenses.CountAsync(e => e.IsPaid && e.FinancialAccountId == null),
                    -(await _context.Expenses.Where(e => e.IsPaid && e.FinancialAccountId == null).SumAsync(e => (decimal?)e.Amount) ?? 0m)),
                ("حركات سلف وديون", "Loans",
                    await _context.LoanTransactions.CountAsync(l => l.FinancialAccountId == null),
                    (await _context.LoanTransactions.Where(l => l.FinancialAccountId == null && (l.Direction == LoanDirection.Repaid || l.Direction == LoanDirection.Borrowed)).SumAsync(l => (decimal?)l.Amount) ?? 0m)
                    - (await _context.LoanTransactions.Where(l => l.FinancialAccountId == null && l.Direction != LoanDirection.Repaid && l.Direction != LoanDirection.Borrowed).SumAsync(l => (decimal?)l.Amount) ?? 0m))
            };
            foreach (var u in unassigned.Where(u => u.Count > 0))
            {
                check.Items.Add(new ReconcilingItem
                {
                    Description = $"{u.Label} بدون حساب ({u.Count} حركة) — تظهر في سطر «بدون حساب»",
                    Reference = "حدّد الحساب في كل حركة حتى يظهر رصيد كل حساب حقيقياً",
                    Amount = u.Net,
                    Category = ReconcilingCategory.Adjustment,
                    LinkController = u.Controller,
                    LinkAction = "Index"
                });
            }

            if (!check.IsMatched)
            {
                check.Items.Insert(0, new ReconcilingItem
                {
                    Description = "فرق غير مفسَّر بين الحسابات وسجل الصندوق (تحقق من حركات باتجاه سلفة غير معروف أو حسابات محذوفة)",
                    Amount = check.Difference,
                    Category = ReconcilingCategory.Investigate
                });
            }

            return check;
        }

        // ===== 2. الذمم ↔ كشوف الحساب =====

        private async Task<ReconciliationCheck> ReceivablesCheckAsync(List<ClinicReceivableVM> receivables)
        {
            var check = new ReconciliationCheck
            {
                Key = "receivables",
                Title = "ذمم المراكز ↔ كشوف الحساب",
                Description = "المتبقي على كل مركز في شاشة الذمم يجب أن يساوي الرصيد الختامي لكشف حسابه.",
                LeftLabel = "مجموع ذمم المراكز",
                LeftValue = receivables.Sum(r => r.Balance),
                RightLabel = "مجموع أرصدة كشوف الحساب",
                FixController = "Finance",
                FixAction = "Receivables",
                FixLabel = "ذمم المراكز"
            };

            // كشف من أول البيانات إلى ما بعد أي تاريخ مستقبلي، حتى يشمل كل الحركات.
            var from = new DateTime(2000, 1, 1);
            var to = new DateTime(2100, 12, 31);

            foreach (var r in receivables)
            {
                if (r.ClinicId == null)
                {
                    // لا يوجد كشف حساب لما ليس له مركز — يظهر كبند يفسّر الفرق.
                    check.Items.Add(new ReconcilingItem
                    {
                        Description = "أعمال أو تحصيلات غير مرتبطة بأي مركز (لا تظهر في أي كشف حساب)",
                        Reference = $"أعمال {r.Invoiced:N2} — تحصيل {r.Collected:N2}",
                        Amount = r.Balance,
                        Category = ReconcilingCategory.Adjustment,
                        LinkController = r.Collected != 0 ? "Income" : "LabCases",
                        LinkAction = "Index"
                    });
                    continue;
                }

                var statement = await _statements.BuildAsync(r.ClinicId.Value, from, to);
                var closing = statement?.ClosingBalance ?? 0m;
                check.RightValue += closing;

                if (Math.Abs(r.Balance - closing) >= 0.005m)
                {
                    check.Items.Add(new ReconcilingItem
                    {
                        Description = $"ذمة المركز لا تطابق كشف حسابه: {r.ClinicName}",
                        Reference = $"الذمم {r.Balance:N2} — الكشف {closing:N2}",
                        Amount = r.Balance - closing,
                        Category = ReconcilingCategory.Investigate,
                        LinkController = "Finance",
                        LinkAction = "Statement",
                        LinkId = r.ClinicId
                    });
                }
            }

            return check;
        }

        // ===== 3 و 4. الأرباح والخسائر ↔ الذمم =====

        private async Task<List<ReconciliationCheck>> ProfitLossChecksAsync(List<ClinicReceivableVM> receivables, DateTime today)
        {
            decimal plRevenue = 0m, plCollections = 0m;
            foreach (var year in await _finance.GetAvailableYearsAsync())
            {
                var summary = await _finance.BuildSummaryAsync(year);
                plRevenue += summary.Revenue;
                plCollections += summary.Collections;
            }

            var revenue = new ReconciliationCheck
            {
                Key = "revenue",
                Title = "الإيراد: الأرباح والخسائر ↔ الذمم",
                Description = "مجموع الإيراد المفوتر في تقرير الأرباح والخسائر لكل السنوات يجب أن يساوي إجمالي أعمال المراكز في الذمم.",
                LeftLabel = "الإيراد في الأرباح والخسائر",
                LeftValue = plRevenue,
                RightLabel = "إجمالي الأعمال في الذمم",
                RightValue = receivables.Sum(r => r.Invoiced),
                FixController = "LabCases",
                FixAction = "Index",
                FixLabel = "الحالات"
            };

            // حالات بسنة/شهر لا يقرؤهما تقرير الأرباح (تسقط منه وتبقى في الذمم)
            var badCases = await _context.LabCases.AsNoTracking()
                .Where(c => c.Status != CaseStatus.Cancelled && (c.Year <= 2000 || c.Month < 1 || c.Month > 12))
                .Select(c => new { c.Id, c.CaseNumber, c.PatientName, c.Year, c.Month, Total = c.Items.Sum(i => i.Quantity * i.UnitPrice) })
                .ToListAsync();
            revenue.Items.AddRange(badCases.Select(c => new ReconcilingItem
            {
                Description = $"حالة بسنة أو شهر غير صحيح ({c.Month}/{c.Year}): {c.PatientName}",
                Reference = c.CaseNumber.HasValue ? $"حالة {c.CaseNumber}" : null,
                Amount = -c.Total,
                Category = ReconcilingCategory.Adjustment,
                LinkController = "LabCases",
                LinkAction = "Edit",
                LinkId = c.Id
            }));

            var collections = new ReconciliationCheck
            {
                Key = "collections",
                Title = "التحصيل: الأرباح والخسائر ↔ الذمم",
                Description = "مجموع التحصيلات المستلمة في تقرير الأرباح والخسائر يجب أن يساوي المحصَّل من المراكز في الذمم.",
                LeftLabel = "التحصيلات في الأرباح والخسائر",
                LeftValue = plCollections,
                RightLabel = "المحصَّل في الذمم",
                RightValue = receivables.Sum(r => r.Collected),
                FixController = "Income",
                FixAction = "Index",
                FixLabel = "الإيرادات"
            };

            var badIncome = await _context.IncomeEntries.AsNoTracking()
                .Where(e => e.IsReceived && (e.Year <= 2000 || e.Month < 1 || e.Month > 12))
                .Select(e => new { e.Id, e.Date, e.Year, e.Month, e.Description, e.Amount })
                .ToListAsync();
            collections.Items.AddRange(badIncome.Select(e => new ReconcilingItem
            {
                Description = $"إيراد بسنة أو شهر غير صحيح ({e.Month}/{e.Year}): {e.Description}",
                Amount = -e.Amount,
                Date = e.Date,
                AgeDays = Age(e.Date, today),
                Category = ReconcilingCategory.Adjustment,
                LinkController = "Income",
                LinkAction = "Edit",
                LinkId = e.Id
            }));

            foreach (var c in new[] { revenue, collections }.Where(c => !c.IsMatched))
            {
                var explained = c.Items.Sum(i => i.Amount);
                var rest = c.Difference - explained;
                if (Math.Abs(rest) >= 0.005m)
                    c.Items.Insert(0, new ReconcilingItem
                    {
                        Description = "فرق غير مفسَّر",
                        Amount = rest,
                        Category = ReconcilingCategory.Investigate
                    });
            }

            return [revenue, collections];
        }

        // ===== 5 و 6 و 7. الفواتير =====

        private async Task<List<ReconciliationCheck>> InvoiceChecksAsync(DateTime today)
        {
            var invoices = await _context.Invoices.AsNoTracking()
                .Select(i => new
                {
                    i.Id,
                    i.Number,
                    i.ClinicId,
                    ClinicName = i.Clinic == null ? null : i.Clinic.Name,
                    i.IssueDate,
                    i.Status,
                    i.TotalAmount,
                    LinesTotal = i.Lines.Sum(l => l.LineTotal),
                    Payments = i.Payments.Select(p => new { p.Id, p.Amount, p.IsReceived, p.ClinicId, p.Date, p.Year, p.Month }).ToList()
                })
                .ToListAsync();

            var caseTotals = await _context.LabCases.AsNoTracking()
                .Where(c => c.InvoiceId != null)
                .GroupBy(c => c.InvoiceId!.Value)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    Total = g.Where(c => c.Status != CaseStatus.Cancelled).SelectMany(c => c.Items).Sum(i => i.Quantity * i.UnitPrice),
                    Cancelled = g.Count(c => c.Status == CaseStatus.Cancelled)
                })
                .ToDictionaryAsync(x => x.InvoiceId);

            var active = invoices.Where(i => i.Status != InvoiceStatus.Cancelled).ToList();
            string Name(int number, string? clinic) => $"فاتورة {number}" + (clinic == null ? "" : $" — {clinic}");

            // 5. إجمالي الفاتورة ↔ أسطرها
            var lines = new ReconciliationCheck
            {
                Key = "invoice-lines",
                Title = "إجمالي الفاتورة ↔ أسطرها",
                Description = "إجمالي كل فاتورة (غير ملغاة) يجب أن يساوي مجموع أسطرها المثبَّتة عند الإصدار.",
                LeftLabel = "مجموع إجماليات الفواتير",
                LeftValue = active.Sum(i => i.TotalAmount),
                RightLabel = "مجموع أسطر الفواتير",
                RightValue = active.Sum(i => i.LinesTotal),
                FixController = "Invoices",
                FixAction = "Index",
                FixLabel = "الفواتير"
            };
            lines.Items.AddRange(active.Where(i => Math.Abs(i.TotalAmount - i.LinesTotal) >= 0.005m).Select(i => new ReconcilingItem
            {
                Description = $"إجمالي الفاتورة لا يساوي أسطرها: {Name(i.Number, i.ClinicName)}",
                Reference = $"الإجمالي {i.TotalAmount:N2} — الأسطر {i.LinesTotal:N2}",
                Amount = i.TotalAmount - i.LinesTotal,
                Date = i.IssueDate,
                AgeDays = Age(i.IssueDate, today),
                Category = ReconcilingCategory.Investigate,
                LinkController = "Invoices",
                LinkAction = "Details",
                LinkId = i.Id
            }));

            // 6. أسطر الفاتورة ↔ الحالات الحالية
            decimal CaseTotal(Guid id) => caseTotals.TryGetValue(id, out var t) ? t.Total : 0m;
            var cases = new ReconciliationCheck
            {
                Key = "invoice-cases",
                Title = "الفواتير ↔ الحالات المفوترة",
                Description = "أسطر الفاتورة نسخة مثبَّتة من حالاتها. أي فرق يعني أن حالة عُدّلت أو أُلغيت بعد الفوترة — راجع هل يلزم رصيد دائن أو تعديل.",
                LeftLabel = "مجموع أسطر الفواتير",
                LeftValue = active.Sum(i => i.LinesTotal),
                RightLabel = "مجموع الحالات الحالية لتلك الفواتير",
                RightValue = active.Sum(i => CaseTotal(i.Id)),
                FixController = "Invoices",
                FixAction = "Index",
                FixLabel = "الفواتير"
            };
            foreach (var i in active)
            {
                var current = CaseTotal(i.Id);
                if (Math.Abs(i.LinesTotal - current) < 0.005m)
                    continue;

                var cancelled = caseTotals.TryGetValue(i.Id, out var t) ? t.Cancelled : 0;
                cases.Items.Add(new ReconcilingItem
                {
                    Description = (cancelled > 0 ? $"{cancelled} حالة أُلغيت بعد الفوترة" : "حالة عُدّلت بعد الفوترة") + $": {Name(i.Number, i.ClinicName)}",
                    Reference = $"الفاتورة {i.LinesTotal:N2} — الحالات الآن {current:N2}",
                    Amount = i.LinesTotal - current,
                    Date = i.IssueDate,
                    AgeDays = Age(i.IssueDate, today),
                    Category = ReconcilingCategory.Investigate,
                    LinkController = "Invoices",
                    LinkAction = "Details",
                    LinkId = i.Id
                });
            }

            // 7. حالة الفاتورة ↔ التحصيلات (نفس قاعدة InvoiceService.RefreshStatusAsync)
            static string Expected(decimal paid, decimal total) =>
                paid <= 0 ? InvoiceStatus.Issued : paid >= total ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

            var status = new ReconciliationCheck
            {
                Key = "invoice-status",
                Title = "حالة الفواتير ↔ التحصيلات",
                Description = "حالة كل فاتورة (صادرة / مسددة جزئياً / مسددة) يجب أن تطابق ما استُلم فعلاً من تحصيلاتها، ولا يتجاوز المستلم إجمالي الفاتورة، ويكون التحصيل لنفس مركز الفاتورة.",
                IsCount = true,
                LeftLabel = "عدد الفواتير غير الملغاة",
                LeftValue = active.Count,
                RightLabel = "فواتير حالتها ومبالغها سليمة",
                FixController = "Invoices",
                FixAction = "Index",
                FixLabel = "الفواتير"
            };

            foreach (var i in invoices)
            {
                var paid = i.Payments.Where(p => p.IsReceived).Sum(p => p.Amount);
                var ok = true;

                if (i.Status == InvoiceStatus.Cancelled)
                {
                    if (paid > 0)
                        status.Items.Add(new ReconcilingItem
                        {
                            Description = $"تحصيل مستلم على فاتورة ملغاة: {Name(i.Number, i.ClinicName)}",
                            Amount = paid,
                            Date = i.IssueDate,
                            AgeDays = Age(i.IssueDate, today),
                            Category = ReconcilingCategory.Adjustment,
                            LinkController = "Invoices",
                            LinkAction = "Details",
                            LinkId = i.Id
                        });
                    continue;
                }

                var expected = Expected(paid, i.TotalAmount);
                // المسودة لا تُحدَّث تلقائياً ما دام لا تحصيل عليها
                if (i.Status != expected && !(i.Status == InvoiceStatus.Draft && paid <= 0))
                {
                    ok = false;
                    status.Items.Add(new ReconcilingItem
                    {
                        Description = $"الحالة «{i.Status}» والصحيح «{expected}»: {Name(i.Number, i.ClinicName)}",
                        Reference = $"المستلم {paid:N2} من {i.TotalAmount:N2}",
                        Amount = i.TotalAmount - paid,
                        Date = i.IssueDate,
                        AgeDays = Age(i.IssueDate, today),
                        Category = ReconcilingCategory.Adjustment,
                        LinkController = "Invoices",
                        LinkAction = "Details",
                        LinkId = i.Id
                    });
                }

                if (paid > i.TotalAmount)
                {
                    ok = false;
                    status.Items.Add(new ReconcilingItem
                    {
                        Description = $"تحصيل أكثر من قيمة الفاتورة: {Name(i.Number, i.ClinicName)}",
                        Reference = $"المستلم {paid:N2} من {i.TotalAmount:N2}",
                        Amount = paid - i.TotalAmount,
                        Date = i.IssueDate,
                        AgeDays = Age(i.IssueDate, today),
                        Category = ReconcilingCategory.Adjustment,
                        LinkController = "Invoices",
                        LinkAction = "Details",
                        LinkId = i.Id
                    });
                }

                foreach (var p in i.Payments.Where(p => p.ClinicId != i.ClinicId))
                {
                    ok = false;
                    status.Items.Add(new ReconcilingItem
                    {
                        Description = $"تحصيل مسجَّل لمركز غير مركز الفاتورة: {Name(i.Number, i.ClinicName)}",
                        Amount = p.Amount,
                        Date = DateOf(p.Date, p.Year, p.Month),
                        AgeDays = Age(DateOf(p.Date, p.Year, p.Month), today),
                        Category = ReconcilingCategory.Investigate,
                        LinkController = "Income",
                        LinkAction = "Edit",
                        LinkId = p.Id
                    });
                }

                if (ok)
                    status.RightValue++;
            }

            return [lines, cases, status];
        }

        // ===== 8. السلف المشطوبة =====

        private async Task<ReconciliationCheck> LoanWriteOffCheckAsync(DateTime today)
        {
            var balances = await _finance.GetLoanBalancesAsync();

            // ما تحمّله تقرير الأرباح: كل سلفة ممنوحة عليها علامة شطب، بكامل مبلغها
            var plWrittenOff = await _context.LoanTransactions
                .Where(l => l.IsWrittenOff && l.Direction == LoanDirection.Given)
                .SumAsync(l => (decimal?)l.Amount) ?? 0m;

            // ما أسقطته أرصدة السلف فعلاً: الشطب لا يتجاوز ما بقي من السلفة (LoanBalanceVM.Balance)
            decimal Effective(LoanBalanceVM b) => Math.Min(b.WrittenOff, Math.Max(0m, b.Given - b.Repaid));

            var check = new ReconciliationCheck
            {
                Key = "loans",
                Title = "الديون المعدومة ↔ أرصدة السلف",
                Description = "ما يُحمَّل على الأرباح كديون معدومة يجب أن يساوي ما أُسقط فعلاً من أرصدة السلف. الشطب يُسقط المتبقي من السلفة فقط، فإن كان الشخص قد سدّد جزءاً منها فالأرباح تحمّل أكثر مما سقط.",
                LeftLabel = "الديون المعدومة في الأرباح",
                LeftValue = plWrittenOff,
                RightLabel = "الشطب الفعلي في أرصدة السلف",
                RightValue = balances.Sum(Effective),
                FixController = "Loans",
                FixAction = "Index",
                FixLabel = "السلف"
            };

            foreach (var b in balances.Where(b => b.WrittenOff - Effective(b) >= 0.005m))
            {
                check.Items.Add(new ReconcilingItem
                {
                    Description = $"شطب أكبر من المتبقي على الشخص: {b.PersonName}",
                    Reference = $"ممنوح {b.Given:N2} — مسدد {b.Repaid:N2} — مشطوب {b.WrittenOff:N2}",
                    Amount = b.WrittenOff - Effective(b),
                    Category = ReconcilingCategory.Adjustment,
                    LinkController = "Loans",
                    LinkAction = "Index"
                });
            }

            // علامة شطب على حركة ليست سلفة ممنوحة — يتجاهلها النظام، فهي غالباً خطأ إدخال
            var ignored = await _context.LoanTransactions.AsNoTracking()
                .Where(l => l.IsWrittenOff && l.Direction != LoanDirection.Given)
                .Select(l => new { l.Id, l.Date, l.Year, l.Month, l.PersonName, l.Direction, l.Amount })
                .ToListAsync();
            check.Items.AddRange(ignored.Select(l => new ReconcilingItem
            {
                Description = $"علامة شطب على «{l.Direction}» (لا أثر لها): {l.PersonName}",
                Reference = "لا يدخل في أي من الرقمين",
                Amount = 0m,
                Date = DateOf(l.Date, l.Year, l.Month),
                AgeDays = Age(DateOf(l.Date, l.Year, l.Month), today),
                Category = ReconcilingCategory.Adjustment,
                LinkController = "Loans",
                LinkAction = "Edit",
                LinkId = l.Id
            }));

            return check;
        }

        // ===== بنود مفتوحة (فروق توقيت) =====

        private async Task<List<ReconcilingItem>> OpenItemsAsync(DateTime today)
        {
            var pendingIncome = await _context.IncomeEntries.AsNoTracking()
                .Where(e => !e.IsReceived)
                .Select(e => new { e.Id, e.Date, e.Year, e.Month, e.Description, e.Amount, Clinic = e.Clinic == null ? null : e.Clinic.Name })
                .ToListAsync();

            var unpaidExpenses = await _context.Expenses.AsNoTracking()
                .Where(e => !e.IsPaid)
                .Select(e => new { e.Id, e.Date, e.Year, e.Month, e.Description, e.Amount })
                .ToListAsync();

            return pendingIncome.Select(e => new ReconcilingItem
                {
                    Description = $"إيراد لم يُستلم: {e.Description}",
                    Reference = e.Clinic,
                    Amount = e.Amount,
                    Date = DateOf(e.Date, e.Year, e.Month),
                    AgeDays = Age(DateOf(e.Date, e.Year, e.Month), today),
                    Category = ReconcilingCategory.Timing,
                    LinkController = "Income",
                    LinkAction = "Edit",
                    LinkId = e.Id
                })
                .Concat(unpaidExpenses.Select(e => new ReconcilingItem
                {
                    Description = $"مصروف لم يُدفع: {e.Description}",
                    Amount = -e.Amount,
                    Date = DateOf(e.Date, e.Year, e.Month),
                    AgeDays = Age(DateOf(e.Date, e.Year, e.Month), today),
                    Category = ReconcilingCategory.Timing,
                    LinkController = "Expenses",
                    LinkAction = "Edit",
                    LinkId = e.Id
                }))
                .OrderByDescending(i => i.AgeDays ?? -1)
                .ToList();
        }

        // ===== أدوات =====

        private static DateTime? DateOf(DateTime? date, int year, int month)
        {
            if (date.HasValue)
                return date.Value.Date;
            if (year > 2000 && month is >= 1 and <= 12)
                return new DateTime(year, month, 1);
            return null;
        }

        private static int? Age(DateTime? date, DateTime today)
            => date.HasValue ? Math.Max(0, (today - date.Value.Date).Days) : null;
    }
}
