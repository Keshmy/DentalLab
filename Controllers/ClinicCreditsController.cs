using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// سجل الرصيد الدائن للعيادات (بعد إلغاء خدمة مدفوعة مسبقاً أو قيد يدوي).
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class ClinicCreditsController : Controller
    {
        private readonly AppDbContext _context;

        public ClinicCreditsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(Guid? clinicId)
        {
            var query = _context.ClinicCreditEntries
                .AsNoTracking()
                .Include(e => e.Clinic)
                .Include(e => e.LabCase)
                .AsQueryable();

            if (clinicId.HasValue)
                query = query.Where(e => e.ClinicId == clinicId.Value);

            var list = await query
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Created)
                .ToListAsync();

            ViewBag.Clinics = new SelectList(
                await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", clinicId);
            ViewBag.ClinicId = clinicId;

            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ClinicCreditEntry
            {
                Date = DateTime.Now.Date
            };
            await FillLookups();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClinicCreditEntry model)
        {
            if (!ModelState.IsValid)
            {
                await FillLookups();
                return View(model);
            }

            model.Id = Guid.NewGuid();
            model.LabCaseId = null;
            model.Created = DateTime.UtcNow;
            if (model.Date == default)
                model.Date = DateTime.Now.Date;

            _context.ClinicCreditEntries.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة قيد الرصيد الدائن";
            return RedirectToAction(nameof(Index), new { clinicId = model.ClinicId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var entry = await _context.ClinicCreditEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (entry == null)
                return View("NotFound");

            var clinicId = entry.ClinicId;
            _context.ClinicCreditEntries.Remove(entry);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف قيد الرصيد الدائن";
            return RedirectToAction(nameof(Index), new { clinicId });
        }

        private async Task FillLookups()
        {
            ViewBag.Clinics = new SelectList(
                await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name");
        }
    }
}
