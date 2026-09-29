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
    }
}
