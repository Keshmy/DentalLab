using DentalLab.Classes;
using DentalLab.Models.Entities;
using DentalLab.Models.Interfaces;
using DentalLab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin,Employee")]
    public class ServiceTypesController : BaseController
    {
        private readonly IUnitOfWork<ServiceType> _services;

        public ServiceTypesController(IUnitOfWork<ServiceType> services, IWebHostEnvironment host) : base(host)
        {
            _services = services;
        }

        public async Task<IActionResult> Index()
            => View(await _services.Repository.GetAll().OrderBy(s => s.Name).ToListAsync());

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceType model)
        {
            if (!ModelState.IsValid)
                return View(model);
            _services.Repository.Insert(model);
            await _services.SaveAsync();
            TempData["SuccessMessage"] = "تم إضافة نوع العمل";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");
            var item = await _services.Repository.GetByIdAsync(id.Value);
            return item == null ? View("NotFound") : View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromServices] RepriceService reprice, Guid id, ServiceType model)
        {
            if (id != model.Id)
                return View("NotFound");
            if (!ModelState.IsValid)
                return View(model);

            var item = await _services.Repository.GetByIdAsync(id);
            if (item == null)
                return View("NotFound");

            var oldPrice = item.UnitPrice;
            var priceChanged = oldPrice != model.UnitPrice;

            // نحدّث الحقول المحرَّرة فقط حتى لا يُمحى تاريخ الإنشاء
            item.Name = model.Name;
            item.UnitPrice = model.UnitPrice;
            item.UnitCost = model.UnitCost;
            item.IsActive = model.IsActive;
            item.PricedPerTooth = model.PricedPerTooth;
            item.Modified = DateTime.UtcNow;

            _services.Repository.Update(item);
            await _services.SaveAsync();
            TempData["SuccessMessage"] = "تم حفظ نوع العمل";

            // السعر العام تغيّر: نعرض الحالات غير المفوترة التي تتأثر (ما ليس لها سعر طبيب/عيادة خاص)
            if (priceChanged && (await reprice.PreviewAsync(new RepriceScope { ServiceTypeId = id })).Count > 0)
            {
                TempData[RepricePrevious.TempDataKey] = RepricePrevious.Encode(
                    new Dictionary<Guid, HashSet<decimal>> { [id] = [oldPrice] });
                TempData["SuccessMessage"] = "تم حفظ نوع العمل. راجع الحالات غير المفوترة التي تتأثر بالسعر الجديد.";
                return RedirectToAction("Index", "Reprice", new { serviceTypeId = id, returnUrl = Url.Action(nameof(Index)) });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _services.Repository.GetByIdAsync(id);
            if (item == null)
                return View("NotFound");
            _services.Repository.Delete(item);
            await _services.SaveAsync();
            TempData["SuccessMessage"] = "تم حذف نوع العمل";
            return RedirectToAction(nameof(Index));
        }
    }
}
