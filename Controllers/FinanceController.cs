using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Services;
using DentalLab.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// تقارير المال: الأرباح والخسائر، الذمم، والإعادات.
    /// كل الأرقام محسوبة من المصدر (أسطر الحالات + المصاريف + الإيرادات) ولا تُدخل يدوياً.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class FinanceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly FinanceService _finance;

        public FinanceController(AppDbContext context, FinanceService finance)
        {
            _context = context;
            _finance = finance;
        }

        /// <summary>الأرباح والخسائر — بالاستحقاق وبالنقد معاً، مع جسر التسوية بينهما.</summary>
        public async Task<IActionResult> Index(int? year, int? month)
        {
            var y = year ?? DateTime.Now.Year;
            var vm = await _finance.BuildSummaryAsync(y, month);
            return View(vm);
        }

        /// <summary>ذمم المراكز: مفوتر − محصَّل.</summary>
        public async Task<IActionResult> Receivables()
        {
            var rows = await _finance.GetClinicReceivablesAsync();
            return View(rows);
        }

        /// <summary>تقرير الإعادات لكل طبيب.</summary>
        public async Task<IActionResult> Remakes(int? year, int? month)
        {
            var y = year ?? DateTime.Now.Year;

            var vm = new RemakeReportVM
            {
                Year = y,
                Month = month,
                AvailableYears = await _finance.GetAvailableYearsAsync()
            };

            var query = _context.LabCaseItems
                .AsNoTracking()
                .Where(i => i.LabCase!.Year == y && i.LabCase.Status != CaseStatus.Cancelled);

            if (month.HasValue)
                query = query.Where(i => i.LabCase!.Month == month.Value);

            var grouped = await query
                .GroupBy(i => new
                {
                    i.LabCase!.DoctorId,
                    DoctorName = i.LabCase.Doctor == null ? null : i.LabCase.Doctor.Name
                })
                .Select(g => new
                {
                    g.Key.DoctorId,
                    g.Key.DoctorName,
                    TotalUnits = g.Sum(x => x.Quantity),
                    RemakeUnits = g.Where(x => x.IsRemake).Sum(x => x.Quantity),
                    ChargedUnits = g.Where(x => x.IsRemake && x.UnitPrice > 0).Sum(x => x.Quantity),
                    FreeUnits = g.Where(x => x.IsRemake && x.UnitPrice == 0).Sum(x => x.Quantity),
                    ChargedAmount = g.Where(x => x.IsRemake).Sum(x => x.Quantity * x.UnitPrice),
                    FreeCost = g.Where(x => x.IsRemake && x.UnitPrice == 0).Sum(x => x.Quantity * x.UnitCost),
                    LostRevenue = g.Where(x => x.IsRemake && x.UnitPrice == 0)
                                   .Sum(x => x.Quantity * (x.ServiceType == null ? 0m : x.ServiceType.UnitPrice))
                })
                .ToListAsync();

            vm.Rows = grouped
                .Where(g => g.RemakeUnits > 0)
                .Select(g => new RemakeRowVM
                {
                    DoctorId = g.DoctorId,
                    DoctorName = g.DoctorName ?? "بدون طبيب",
                    TotalUnits = g.TotalUnits,
                    RemakeUnits = g.RemakeUnits,
                    ChargedUnits = g.ChargedUnits,
                    FreeUnits = g.FreeUnits,
                    ChargedAmount = g.ChargedAmount,
                    FreeCost = g.FreeCost,
                    LostRevenue = g.LostRevenue
                })
                .OrderByDescending(r => r.RemakeUnits)
                .ToList();

            var reasons = await query
                .Where(i => i.IsRemake && i.RemakeReason != null)
                .GroupBy(i => i.RemakeReason!)
                .Select(g => new { Reason = g.Key, Units = g.Sum(x => x.Quantity) })
                .ToListAsync();

            vm.ByReason = reasons
                .OrderByDescending(r => r.Units)
                .ToDictionary(r => r.Reason, r => r.Units);

            return View(vm);
        }

        /// <summary>مطابقة الحسابات: يقارن أرقام الشاشات المالية ببعضها ويسرد بنود أي فرق (للقراءة فقط).</summary>
        [HttpGet]
        public async Task<IActionResult> Reconciliation([FromServices] ReconciliationService reconciliation)
            => View(await reconciliation.BuildAsync());

        /// <summary>كشف حساب مركز: الأعمال والتحصيلات والرصيد الدائن برصيد متحرك.</summary>
        [HttpGet]
        public async Task<IActionResult> Statement(Guid? clinicId, DateTime? from, DateTime? to, [FromServices] StatementService statements)
        {
            ViewBag.Clinics = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "Id", "Name", clinicId);

            if (clinicId == null)
                return View((ClinicStatementVM?)null);

            var vm = await statements.BuildAsync(clinicId.Value, from, to);
            return vm == null ? View("NotFound") : View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportStatement(Guid clinicId, DateTime? from, DateTime? to, string? format,
            [FromServices] StatementService statements, [FromServices] ExportService export)
        {
            var vm = await statements.BuildAsync(clinicId, from, to);
            if (vm == null)
                return View("NotFound");

            var report = new ReportTable
            {
                Title = "كشف حساب",
                Subtitle = vm.ClinicName,
                FileName = $"Statement-{DateTime.Now:yyyy-MM-dd}",
                Info =
                [
                    ("المركز / العيادة", vm.ClinicName),
                    ("الهاتف", vm.ClinicPhone ?? "—"),
                    ("من تاريخ", vm.From.ToString("dd/MM/yyyy")),
                    ("إلى تاريخ", vm.To.ToString("dd/MM/yyyy"))
                ],
                Headers = ["التاريخ", "النوع", "البيان", "المرجع", "مدين", "دائن", "الرصيد"],
                ColumnWidths = [1.1f, 1, 3.4f, 1.4f, 1.2f, 1.2f, 1.3f],
                Rows = new List<object?[]> { new object?[] { vm.From, null, "رصيد سابق", null, null, null, vm.OpeningBalance } }
                    .Concat(vm.Lines.Select(l => new object?[]
                    {
                        l.Date, l.Kind, l.Description, l.Reference,
                        l.Debit == 0 ? null : l.Debit, l.Credit == 0 ? null : l.Credit, l.Balance
                    })).ToList(),
                Footer = [null, null, "الإجمالي", null, vm.TotalDebit, vm.TotalCredit, vm.ClosingBalance],
                Summary =
                [
                    ("رصيد سابق", vm.OpeningBalance.ToString("N2")),
                    ("إجمالي الأعمال", vm.TotalDebit.ToString("N2")),
                    ("إجمالي المدفوع والرصيد الدائن", vm.TotalCredit.ToString("N2")),
                    (vm.ClosingBalance >= 0 ? "المستحق على المركز" : "رصيد لصالح المركز", Math.Abs(vm.ClosingBalance).ToString("N2"))
                ]
            };

            var (content, type, name) = await export.ExportAsync(report, ExportService.ParseFormat(format));
            return File(content, type, name);
        }

        /// <summary>تصدير الأرباح والخسائر (تفصيل الأشهر).</summary>
        [HttpGet]
        public async Task<IActionResult> ExportSummary(int? year, int? month, string? format, [FromServices] ExportService export)
        {
            var y = year ?? DateTime.Now.Year;
            var vm = await _finance.BuildSummaryAsync(y, month);
            var period = month.HasValue ? $"{month}/{y}" : $"سنة {y}";

            var report = new ReportTable
            {
                Title = "الأرباح والخسائر",
                Subtitle = period,
                FileName = month.HasValue ? $"ProfitLoss-{y}-{month:00}" : $"ProfitLoss-{y}",
                Headers = ["الشهر", "الوحدات", "الإيراد", "مصروفات", "الربح قبل النسبة", "النسبة", "صافي الربح", "محصَّل", "لم يُحصَّل", "صافي النقد", "إعادات"],
                Rows = vm.Months.Select(m => new object?[]
                {
                    m.MonthName, m.Units, m.Revenue, m.OperatingExpenses, m.AccrualProfit, m.PartnerShare,
                    m.NetProfit, m.Collections, m.PendingIncome, m.CashProfit, m.RemakeUnits
                }).ToList(),
                Footer =
                [
                    "الإجمالي", vm.Units, vm.Revenue, vm.OperatingExpenses, vm.AccrualProfit, vm.PartnerShare,
                    vm.NetProfit, vm.Collections, vm.PendingIncome, vm.CashProfit, vm.RemakeUnits
                ],
                Summary =
                [
                    ("إجمالي الذمم على المراكز", vm.TotalReceivables.ToString("N2")),
                    ("رصيد السلف", vm.TotalLoanBalance.ToString("N2"))
                ]
            };

            var (content, type, name) = await export.ExportAsync(report, ExportService.ParseFormat(format));
            return File(content, type, name);
        }

        /// <summary>تصدير ذمم المراكز.</summary>
        [HttpGet]
        public async Task<IActionResult> ExportReceivables(string? format, [FromServices] ExportService export)
        {
            var rows = await _finance.GetClinicReceivablesAsync();

            var report = new ReportTable
            {
                Title = "ذمم المراكز",
                Subtitle = $"حتى تاريخ {DateTime.Now:dd/MM/yyyy}",
                FileName = $"Receivables-{DateTime.Now:yyyy-MM-dd}",
                Headers = ["المركز", "الحالات", "الوحدات", "رصيد افتتاحي", "إجمالي الأعمال", "المحصَّل", "رصيد دائن", "المتبقي"],
                ColumnWidths = [2.5f, 1, 1, 1.3f, 1.4f, 1.3f, 1.2f, 1.4f],
                Rows = rows.Select(r => new object?[]
                {
                    r.ClinicName, r.CaseCount, r.Units, r.OpeningBalance, r.Invoiced, r.Collected, r.CreditBalance, r.Balance
                }).ToList(),
                Footer =
                [
                    "الإجمالي", rows.Sum(r => r.CaseCount), rows.Sum(r => r.Units), rows.Sum(r => r.OpeningBalance),
                    rows.Sum(r => r.Invoiced), rows.Sum(r => r.Collected), rows.Sum(r => r.CreditBalance), rows.Sum(r => r.Balance)
                ],
                Note = "المتبقي = رصيد افتتاحي + إجمالي الأعمال − المحصَّل − الرصيد الدائن"
            };

            var (content, type, name) = await export.ExportAsync(report, ExportService.ParseFormat(format));
            return File(content, type, name);
        }
    }
}
