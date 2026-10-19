using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// الأسعار الخاصة بالعيادات/المراكز.
    /// السعر المطبَّق = سعر الطبيب إن وُجد، وإلا سعر العيادة، وإلا السعر العام.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class ClinicPricesController : Controller
    {
        private readonly AppDbContext _context;

        public ClinicPricesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var clinics = await _context.Clinics
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new ClinicPriceSummaryVM
                {
                    ClinicId = c.Id,
                    ClinicName = c.Name,
                    OverrideCount = c.ClinicPrices.Count,
                    CaseCount = c.LabCases.Count,
                    Revenue = c.LabCases
                        .Where(x => x.Status != CaseStatus.Cancelled)
                        .SelectMany(x => x.Items)
                        .Sum(i => i.Quantity * i.UnitPrice),
                    MaterialCost = c.LabCases
                        .Where(x => x.Status != CaseStatus.Cancelled)
                        .SelectMany(x => x.Items)
                        .Sum(i => i.Quantity * i.UnitCost)
                })
                .ToListAsync();

            return View(clinics.OrderByDescending(c => c.Revenue).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Manage(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var clinic = await _context.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (clinic == null)
                return View("NotFound");

            return View(await BuildSheetAsync(clinic));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(Guid id, ClinicPriceSheetVM model)
        {
            var clinic = await _context.Clinics.FirstOrDefaultAsync(c => c.Id == id);
            if (clinic == null)
                return View("NotFound");

            if (!ModelState.IsValid)
                return View(await BuildSheetAsync(clinic));

            var existing = await _context.ClinicPrices
                .Where(p => p.ClinicId == id)
                .ToListAsync();

            var services = await _context.ServiceTypes
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Id, s => s.UnitPrice);

            int added = 0, updated = 0, removed = 0;

            foreach (var row in model.Rows)
            {
                if (!services.TryGetValue(row.ServiceTypeId, out var generalPrice))
                    continue;

                var current = existing.FirstOrDefault(p => p.ServiceTypeId == row.ServiceTypeId);
                var wantsOverride = row.SpecialPrice.HasValue && row.SpecialPrice.Value != generalPrice;

                if (wantsOverride)
                {
                    if (current == null)
                    {
                        _context.ClinicPrices.Add(new ClinicPrice
                        {
                            ClinicId = id,
                            ServiceTypeId = row.ServiceTypeId,
                            Price = row.SpecialPrice!.Value,
                            Created = DateTime.UtcNow
                        });
                        added++;
                    }
                    else if (current.Price != row.SpecialPrice.Value)
                    {
                        current.Price = row.SpecialPrice.Value;
                        current.Modified = DateTime.UtcNow;
                        updated++;
                    }
                }
                else if (current != null)
                {
                    _context.ClinicPrices.Remove(current);
                    removed++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"أسعار {clinic.Name}: أُضيف {added}، عُدِّل {updated}، حُذف {removed}. " +
                "الحالات المسجَّلة سابقاً لم تتغير — كل سطر يحفظ سعره وقت الإدخال.";

            return RedirectToAction(nameof(Manage), new { id });
        }

        private async Task<ClinicPriceSheetVM> BuildSheetAsync(Clinic clinic)
        {
            var services = await _context.ServiceTypes
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var overrides = await _context.ClinicPrices
                .AsNoTracking()
                .Where(p => p.ClinicId == clinic.Id)
                .ToDictionaryAsync(p => p.ServiceTypeId, p => p.Price);

            return new ClinicPriceSheetVM
            {
                ClinicId = clinic.Id,
                ClinicName = clinic.Name,
                Rows = services.Select(s => new ClinicPriceRowVM
                {
                    ServiceTypeId = s.Id,
                    ServiceName = s.Name,
                    GeneralPrice = s.UnitPrice,
                    UnitCost = s.UnitCost,
                    SpecialPrice = overrides.TryGetValue(s.Id, out var p) ? p : null
                }).ToList()
            };
        }
    }
}
