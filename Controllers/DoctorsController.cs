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
    public class DoctorsController : BaseController
    {
        private readonly IUnitOfWork<Doctor> _doctors;

        public DoctorsController(IUnitOfWork<Doctor> doctors, IWebHostEnvironment host) : base(host)
        {
            _doctors = doctors;
        }

        public async Task<IActionResult> Index()
            => View(await _doctors.Repository.GetAll().OrderBy(d => d.Name).ToListAsync());

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Doctor model)
        {
            if (!ModelState.IsValid)
                return View(model);
            _doctors.Repository.Insert(model);
            await _doctors.SaveAsync();
            TempData["SuccessMessage"] = "تم إضافة الطبيب";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");
            var item = await _doctors.Repository.GetByIdAsync(id.Value);
            return item == null ? View("NotFound") : View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Doctor model)
        {
            if (id != model.Id)
                return View("NotFound");
            if (!ModelState.IsValid)
                return View(model);
            model.Modified = DateTime.UtcNow;
            _doctors.Repository.Update(model);
            await _doctors.SaveAsync();
            TempData["SuccessMessage"] = "تم حفظ الطبيب";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _doctors.Repository.GetByIdAsync(id);
            if (item == null)
                return View("NotFound");
            _doctors.Repository.Delete(item);
            await _doctors.SaveAsync();
            TempData["SuccessMessage"] = "تم حذف الطبيب";
            return RedirectToAction(nameof(Index));
        }
    }
}
