using DentalLab.Classes;
using DentalLab.Models.Entities;
using DentalLab.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin,Employee")]
    public class ClinicsController : BaseController
    {
        private readonly IUnitOfWork<Clinic> _clinics;

        public ClinicsController(IUnitOfWork<Clinic> clinics, IWebHostEnvironment host) : base(host)
        {
            _clinics = clinics;
        }

        public async Task<IActionResult> Index()
            => View(await _clinics.Repository.GetAll().OrderBy(c => c.Name).ToListAsync());

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Clinic model)
        {
            if (!ModelState.IsValid)
                return View(model);
            _clinics.Repository.Insert(model);
            await _clinics.SaveAsync();
            TempData["SuccessMessage"] = "تم إضافة العيادة";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");
            var item = await _clinics.Repository.GetByIdAsync(id.Value);
            return item == null ? View("NotFound") : View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Clinic model)
        {
            if (id != model.Id)
                return View("NotFound");
            if (!ModelState.IsValid)
                return View(model);
            model.Modified = DateTime.UtcNow;
            _clinics.Repository.Update(model);
            await _clinics.SaveAsync();
            TempData["SuccessMessage"] = "تم حفظ العيادة";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _clinics.Repository.GetByIdAsync(id);
            if (item == null)
                return View("NotFound");
            _clinics.Repository.Delete(item);
            await _clinics.SaveAsync();
            TempData["SuccessMessage"] = "تم حذف العيادة";
            return RedirectToAction(nameof(Index));
        }
    }
}
