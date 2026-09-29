using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Services;
using DentalLab.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin,Employee")]
    public class LabCasesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PricingService _pricing;

        public LabCasesController(AppDbContext context, PricingService pricing)
        {
            _context = context;
            _pricing = pricing;
        }

        public async Task<IActionResult> Index(int? year, int? month, Guid? doctorId, Guid? clinicId, string? status, string? search)
        {
            var query = _context.LabCases
                .AsNoTracking()
                .Include(c => c.Doctor)
                .Include(c => c.Clinic)
                .Include(c => c.Items).ThenInclude(i => i.ServiceType)
                .AsQueryable();

            if (year.HasValue)
                query = query.Where(c => c.Year == year.Value);
            if (month.HasValue)
                query = query.Where(c => c.Month == month.Value);
            if (doctorId.HasValue)
                query = query.Where(c => c.DoctorId == doctorId.Value);
            if (clinicId.HasValue)
                query = query.Where(c => c.ClinicId == clinicId.Value);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(c => c.Status == status);
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.PatientName.Contains(search)
                                         || (c.Notes != null && c.Notes.Contains(search))
                                         || c.Items.Any(i => i.ProductName != null && i.ProductName.Contains(search))
                                         || c.Items.Any(i => i.ServiceType != null && i.ServiceType.Name.Contains(search)));
            }

            var list = await query
                .OrderByDescending(c => c.Year)
                .ThenByDescending(c => c.Month)
                .ThenByDescending(c => c.CaseNumber)
                .ToListAsync();

            await FillLookups(year, month, doctorId, clinicId, status);
            ViewBag.Search = search;
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await FillLookups();
            var nextNumber = (await _context.LabCases.MaxAsync(c => (int?)c.CaseNumber) ?? 0) + 1;
            var now = DateTime.Now;

            var vm = new LabCaseFormVM
            {
                CaseNumber = nextNumber,
                ReceivedDate = now.Date,
                Year = now.Year,
                Month = now.Month,
                Status = CaseStatus.Normal,
                Items = [new LabCaseItemFormVM()]
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LabCaseFormVM model)
        {
            ApplyDates(model);
            var lines = model.Items.Where(i => !i.IsEmpty).ToList();

            if (lines.Count == 0)
                ModelState.AddModelError(string.Empty, "أضف سطراً واحداً على الأقل (المنتج والعدد والسعر).");

            if (!ModelState.IsValid)
            {
                await FillLookups();
                if (model.Items.Count == 0)
                    model.Items.Add(new LabCaseItemFormVM());
                return View(model);
            }

            var labCase = new LabCase
            {
                Id = Guid.NewGuid(),
                CaseNumber = model.CaseNumber,
                PatientName = model.PatientName,
                DoctorId = model.DoctorId,
                ClinicId = model.ClinicId,
                ReceivedDate = model.ReceivedDate,
                DeliveryDate = model.DeliveryDate,
                ToothPosition = model.ToothPosition,
                DocumentNumber = model.DocumentNumber,
                ReceiptNumber = model.ReceiptNumber,
                Notes = model.Notes,
                Year = model.Year,
                Month = model.Month,
                Status = model.Status,
                Created = DateTime.UtcNow
            };

            await BuildItems(labCase, lines);

            _context.LabCases.Add(labCase);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم إضافة الحالة — الإجمالي {labCase.Total:N2}";
            return RedirectToAction(nameof(Details), new { id = labCase.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var labCase = await _context.LabCases
                .Include(c => c.Items).ThenInclude(i => i.ServiceType)
                .Include(c => c.Invoice)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (labCase == null)
                return View("NotFound");

            await FillLookups();
            return View(ToForm(labCase));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, LabCaseFormVM model)
        {
            if (id != model.Id)
                return View("NotFound");

            var labCase = await _context.LabCases
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (labCase == null)
                return View("NotFound");

            ApplyDates(model);
            var lines = model.Items.Where(i => !i.IsEmpty).ToList();

            if (lines.Count == 0)
                ModelState.AddModelError(string.Empty, "أضف سطراً واحداً على الأقل (المنتج والعدد والسعر).");

            if (!ModelState.IsValid)
            {
                await FillLookups();
                model.IsInvoiced = labCase.InvoiceId.HasValue;
                if (model.Items.Count == 0)
                    model.Items.Add(new LabCaseItemFormVM());
                return View(model);
            }

            labCase.CaseNumber = model.CaseNumber;
            labCase.PatientName = model.PatientName;
            labCase.DoctorId = model.DoctorId;
            labCase.ClinicId = model.ClinicId;
            labCase.ReceivedDate = model.ReceivedDate;
            labCase.DeliveryDate = model.DeliveryDate;
            labCase.ToothPosition = model.ToothPosition;
            labCase.DocumentNumber = model.DocumentNumber;
            labCase.ReceiptNumber = model.ReceiptNumber;
            labCase.Notes = model.Notes;
            labCase.Year = model.Year;
            labCase.Month = model.Month;
            labCase.Status = model.Status;
            labCase.Modified = DateTime.UtcNow;

            _context.LabCaseItems.RemoveRange(labCase.Items);
            labCase.Items.Clear();
            await BuildItems(labCase, lines);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم حفظ الحالة — الإجمالي {labCase.Total:N2}";
            return RedirectToAction(nameof(Details), new { id = labCase.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var labCase = await _context.LabCases
                .AsNoTracking()
                .Include(c => c.Doctor)
                .Include(c => c.Clinic)
                .Include(c => c.Invoice)
                .Include(c => c.Items).ThenInclude(i => i.ServiceType)
                .FirstOrDefaultAsync(c => c.Id == id);

            return labCase == null ? View("NotFound") : View(labCase);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var labCase = await _context.LabCases.FirstOrDefaultAsync(c => c.Id == id);
            if (labCase == null)
                return View("NotFound");

            if (labCase.InvoiceId.HasValue)
            {
                TempData["ErrorMessage"] = "لا يمكن حذف حالة مُفوترة. ألغِ الفاتورة أولاً.";
                return RedirectToAction(nameof(Index));
            }

            _context.LabCases.Remove(labCase);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الحالة";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>سعر نوع العمل لهذا الطبيب (الخاص إن وُجد، وإلا العام) + التكلفة.</summary>
        public async Task<JsonResult> ServicePrice(Guid id, Guid? doctorId)
        {
            var result = await _pricing.ResolveAsync(id, doctorId);
            var cost = await _context.ServiceTypes
                .Where(s => s.Id == id)
                .Select(s => s.UnitCost)
                .FirstOrDefaultAsync();

            return Json(new
            {
                price = result.Price,
                generalPrice = result.GeneralPrice,
                isDoctorPrice = result.IsDoctorPrice,
                cost,
                name = result.ServiceName
            });
        }

        /// <summary>سعر الإعادة المقترح لهذه الحالة (الأولى على المعمل، والتالية على الطبيب).</summary>
        public async Task<JsonResult> RemakePrice(Guid? caseId, Guid serviceTypeId, Guid? doctorId, string? reason)
        {
            var result = await _pricing.ResolveRemakeAsync(caseId ?? Guid.Empty, serviceTypeId, doctorId, reason);
            return Json(new
            {
                price = result.Price,
                fullPrice = result.FullPrice,
                chargeable = result.IsChargeable,
                sequence = result.Sequence,
                explanation = result.Explanation
            });
        }

        /// <summary>ينسخ الأسطر إلى الحالة مع تثبيت السعر والتكلفة وترقيم الإعادات.</summary>
        private async Task BuildItems(LabCase labCase, List<LabCaseItemFormVM> lines)
        {
            var serviceIds = lines.Where(l => l.ServiceTypeId.HasValue)
                                  .Select(l => l.ServiceTypeId!.Value)
                                  .Distinct()
                                  .ToList();

            var services = await _context.ServiceTypes
                .AsNoTracking()
                .Where(s => serviceIds.Contains(s.Id))
                .ToListAsync();

            var doctorPrices = labCase.DoctorId.HasValue
                ? await _context.DoctorPrices
                    .AsNoTracking()
                    .Where(p => p.DoctorId == labCase.DoctorId.Value && serviceIds.Contains(p.ServiceTypeId))
                    .ToDictionaryAsync(p => p.ServiceTypeId, p => p.Price)
                : [];

            var remakeSequence = 0;

            foreach (var line in lines)
            {
                var service = services.FirstOrDefault(s => s.Id == line.ServiceTypeId);

                // السعر المُدخل هو المرجع؛ وإن تُرك صفراً لغير الإعادة نأخذ السعر المطبَّق
                var price = line.UnitPrice;
                if (price == 0 && !line.IsRemake && service != null)
                {
                    price = doctorPrices.TryGetValue(service.Id, out var special)
                        ? special
                        : service.UnitPrice;
                }

                var cost = line.UnitCost > 0
                    ? line.UnitCost
                    : service?.UnitCost ?? 0m;

                if (line.IsRemake)
                    remakeSequence++;

                labCase.Items.Add(new LabCaseItem
                {
                    Id = Guid.NewGuid(),
                    LabCaseId = labCase.Id,
                    ServiceTypeId = line.ServiceTypeId,
                    ProductName = string.IsNullOrWhiteSpace(line.ProductName) ? service?.Name : line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = price,
                    UnitCost = cost,
                    IsRemake = line.IsRemake,
                    RemakeReason = line.IsRemake ? line.RemakeReason : null,
                    RemakeSequence = line.IsRemake ? remakeSequence : 0,
                    ToothPosition = line.ToothPosition,
                    Notes = line.Notes,
                    Created = DateTime.UtcNow
                });
            }
        }

        private static LabCaseFormVM ToForm(LabCase labCase) => new()
        {
            Id = labCase.Id,
            CaseNumber = labCase.CaseNumber,
            PatientName = labCase.PatientName,
            DoctorId = labCase.DoctorId,
            ClinicId = labCase.ClinicId,
            ReceivedDate = labCase.ReceivedDate,
            DeliveryDate = labCase.DeliveryDate,
            ToothPosition = labCase.ToothPosition,
            DocumentNumber = labCase.DocumentNumber,
            ReceiptNumber = labCase.ReceiptNumber,
            Notes = labCase.Notes,
            Year = labCase.Year,
            Month = labCase.Month,
            Status = labCase.Status,
            IsInvoiced = labCase.InvoiceId.HasValue,
            InvoiceNumber = labCase.Invoice?.Number,
            Items = labCase.Items
                .OrderBy(i => i.IsRemake)
                .ThenBy(i => i.Created)
                .Select(i => new LabCaseItemFormVM
                {
                    Id = i.Id,
                    ServiceTypeId = i.ServiceTypeId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    UnitCost = i.UnitCost,
                    IsRemake = i.IsRemake,
                    RemakeReason = i.RemakeReason,
                    ToothPosition = i.ToothPosition,
                    Notes = i.Notes
                }).ToList()
        };

        private static void ApplyDates(LabCaseFormVM model)
        {
            var d = model.ReceivedDate ?? DateTime.Now;
            if (model.Year == 0)
                model.Year = d.Year;
            if (model.Month == 0)
                model.Month = d.Month;
        }

        private async Task FillLookups(int? year = null, int? month = null, Guid? doctorId = null, Guid? clinicId = null, string? status = null)
        {
            ViewBag.Doctors = new SelectList(await _context.Doctors.AsNoTracking().OrderBy(d => d.Name).ToListAsync(), "Id", "Name", doctorId);
            ViewBag.Clinics = new SelectList(await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "Id", "Name", clinicId);
            var services = await _context.ServiceTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            ViewBag.Services = new SelectList(services, "Id", "Name");
            ViewBag.ServiceList = services;
            ViewBag.Statuses = new SelectList(CaseStatus.All, status);
            ViewBag.RemakeReasons = new SelectList(RemakeReason.All);
            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.DoctorId = doctorId;
            ViewBag.ClinicId = clinicId;
            ViewBag.Status = status;
        }
    }
}
