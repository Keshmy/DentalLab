using DentalLab.Services;
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
    /// الأسعار الخاصة بالأطباء. القاعدة:
    ///   السعر المطبَّق = السعر الخاص بالطبيب إن وُجد، وإلا السعر العام للمنتج.
    /// مثال: السعر العام 130، ولطبيب معيّن 120 → تُفوتر حالاته بـ 120.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class DoctorPricesController : Controller
    {
        private readonly AppDbContext _context;

        public DoctorPricesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var doctors = await _context.Doctors
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new DoctorPriceSummaryVM
                {
                    DoctorId = d.Id,
                    DoctorName = d.Name,
                    OverrideCount = d.DoctorPrices.Count,
                    CaseCount = d.LabCases.Count,
                    Revenue = d.LabCases
                        .Where(c => c.Status != CaseStatus.Cancelled)
                        .SelectMany(c => c.Items)
                        .Sum(i => i.Quantity * i.UnitPrice),
                    MaterialCost = d.LabCases
                        .Where(c => c.Status != CaseStatus.Cancelled)
                        .SelectMany(c => c.Items)
                        .Sum(i => i.Quantity * i.UnitCost),
                    RemakeUnits = d.LabCases
                        .SelectMany(c => c.Items)
                        .Where(i => i.IsRemake)
                        .Sum(i => i.Quantity),
                    FreeRemakeCost = d.LabCases
                        .SelectMany(c => c.Items)
                        .Where(i => i.IsRemake && i.UnitPrice == 0)
                        .Sum(i => i.Quantity * i.UnitCost)
                })
                .ToListAsync();

            return View(doctors.OrderByDescending(d => d.Revenue).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> Manage(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var doctor = await _context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            if (doctor == null)
                return View("NotFound");

            return View(await BuildSheetAsync(doctor));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage([FromServices] RepriceService reprice, Guid id, DoctorPriceSheetVM model)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == id);
            if (doctor == null)
                return View("NotFound");

            if (!ModelState.IsValid)
                return View(await BuildSheetAsync(doctor));

            var existing = await _context.DoctorPrices
                .Where(p => p.DoctorId == id)
                .ToListAsync();

            var services = await _context.ServiceTypes
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Id, s => s.UnitPrice);

            int added = 0, updated = 0, removed = 0;
            var previous = new Dictionary<Guid, HashSet<decimal>>();

            foreach (var row in model.Rows)
            {
                if (!services.TryGetValue(row.ServiceTypeId, out var generalPrice))
                    continue;

                var current = existing.FirstOrDefault(p => p.ServiceTypeId == row.ServiceTypeId);
                var before = new HashSet<decimal> { generalPrice };
                if (current != null) before.Add(current.Price);

                // سعر خاص مساوٍ للعام لا معنى له — نحذفه ليبقى الطبيب على السعر العام
                var wantsOverride = row.SpecialPrice.HasValue && row.SpecialPrice.Value != generalPrice;

                if (wantsOverride)
                {
                    if (current == null)
                    {
                        _context.DoctorPrices.Add(new DoctorPrice
                        {
                            DoctorId = id,
                            ServiceTypeId = row.ServiceTypeId,
                            Price = row.SpecialPrice!.Value,
                            Created = DateTime.UtcNow
                        });
                        added++;
                        previous[row.ServiceTypeId] = before;
                    }
                    else if (current.Price != row.SpecialPrice.Value)
                    {
                        current.Price = row.SpecialPrice.Value;
                        current.Modified = DateTime.UtcNow;
                        updated++;
                        previous[row.ServiceTypeId] = before;
                    }
                }
                else if (current != null)
                {
                    _context.DoctorPrices.Remove(current);
                    removed++;
                    previous[row.ServiceTypeId] = before;
                }
            }

            await _context.SaveChangesAsync();

            var message = $"أسعار {doctor.Name}: أُضيف {added}، عُدِّل {updated}، حُذف {removed}.";

            // الحالات المفوترة لا تتغير أبداً؛ غير المفوترة تُعرض للمراجعة قبل تحديث سعرها
            if (added + updated + removed > 0
                && (await reprice.PreviewAsync(new RepriceScope { DoctorId = id })).Count > 0)
            {
                TempData[RepricePrevious.TempDataKey] = RepricePrevious.Encode(previous);
                TempData["SuccessMessage"] = message + " راجع الحالات غير المفوترة التي تتأثر بهذا التعديل.";
                return RedirectToAction("Index", "Reprice", new { doctorId = id, returnUrl = Url.Action(nameof(Manage), new { id }) });
            }

            TempData["SuccessMessage"] = message + " الحالات المفوترة لا تتغير.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        private async Task<DoctorPriceSheetVM> BuildSheetAsync(Doctor doctor)
        {
            var services = await _context.ServiceTypes
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var overrides = await _context.DoctorPrices
                .AsNoTracking()
                .Where(p => p.DoctorId == doctor.Id)
                .ToDictionaryAsync(p => p.ServiceTypeId, p => p.Price);

            return new DoctorPriceSheetVM
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Rows = services.Select(s => new DoctorPriceRowVM
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
