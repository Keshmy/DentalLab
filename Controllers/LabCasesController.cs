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
            var list = await FilterAsync(year, month, doctorId, clinicId, status, search);
            await FillLookups(year, month, doctorId, clinicId, status);
            ViewBag.Search = search;
            return View(list);
        }

        /// <summary>مواعيد التسليم: المتأخرة، المستحقة اليوم، والقادمة خلال 7 أيام.</summary>
        [HttpGet]
        public async Task<IActionResult> Due()
        {
            var today = DateTime.Today;
            var horizon = today.AddDays(8);

            var pending = await _context.LabCases
                .AsNoTracking()
                .Include(c => c.Doctor)
                .Include(c => c.Clinic)
                .Include(c => c.Items).ThenInclude(i => i.ServiceType)
                .Where(c => c.DeliveredAt == null
                            && c.Status != CaseStatus.Cancelled
                            && c.DeliveryDate != null
                            && c.DeliveryDate < horizon)
                .OrderBy(c => c.DeliveryDate)
                .ThenBy(c => c.CaseNumber)
                .ToListAsync();

            var deliveredToday = await _context.LabCases
                .AsNoTracking()
                .Include(c => c.Doctor)
                .Include(c => c.Clinic)
                .Include(c => c.Items).ThenInclude(i => i.ServiceType)
                .Where(c => c.DeliveredAt >= today && c.DeliveredAt < today.AddDays(1))
                .OrderByDescending(c => c.DeliveredAt)
                .ToListAsync();

            var vm = new DeliveryDueVM
            {
                Today = today,
                Overdue = pending.Where(c => c.DeliveryDate!.Value.Date < today).ToList(),
                DueToday = pending.Where(c => c.DeliveryDate!.Value.Date == today).ToList(),
                Upcoming = pending.Where(c => c.DeliveryDate!.Value.Date > today).ToList(),
                DeliveredToday = deliveredToday,
                WithoutDate = await _context.LabCases.CountAsync(c => c.DeliveredAt == null
                                                                      && c.Status != CaseStatus.Cancelled
                                                                      && c.DeliveryDate == null
                                                                      && c.ReceivedDate >= today.AddDays(-30))
            };

            return View(vm);
        }

        /// <summary>تأشير الحالة كمُسلَّمة (أو إلغاء التأشير).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDelivered(Guid id, bool delivered = true, string? returnTo = null)
        {
            var labCase = await _context.LabCases.FirstOrDefaultAsync(c => c.Id == id);
            if (labCase == null)
                return View("NotFound");

            labCase.DeliveredAt = delivered ? DateTime.Now : null;
            labCase.Modified = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = delivered
                ? $"تم تسليم الحالة رقم {labCase.CaseNumber} — {labCase.PatientName}"
                : $"أُلغي تأشير التسليم للحالة رقم {labCase.CaseNumber}";

            return returnTo == "details"
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToAction(nameof(Due));
        }

        /// <summary>تصدير قائمة الحالات بنفس فلاتر الصفحة.</summary>
        [HttpGet]
        public async Task<IActionResult> Export(int? year, int? month, Guid? doctorId, Guid? clinicId, string? status, string? search,
            string? format, [FromServices] ExportService export)
        {
            var list = await FilterAsync(year, month, doctorId, clinicId, status, search);
            var billable = list.Where(c => c.IsBillable).ToList();

            var report = new ReportTable
            {
                Title = "الحالات",
                Subtitle = year.HasValue ? (month.HasValue ? $"شهر {month}/{year}" : $"سنة {year}") : "كل الفترات",
                FileName = $"Cases-{DateTime.Now:yyyy-MM-dd}",
                Headers = ["رقم", "اسم الحالة", "الطبيب", "المركز", "الأعمال", "الاستلام", "التسليم", "الحالة", "الوحدات", "الإجمالي", "مفوترة", "ملاحظات"],
                ColumnWidths = [0.7f, 2, 1.6f, 1.6f, 3, 1.1f, 1.1f, 0.9f, 0.8f, 1.1f, 0.8f, 2],
                Rows = list.Select(c => new object?[]
                {
                    c.CaseNumber, c.PatientName, c.Doctor?.Name, c.Clinic?.Name,
                    string.Join("، ", c.Items.Select(i => i.Quantity > 1 ? $"{i.DisplayName} ×{i.Quantity}" : i.DisplayName)),
                    c.ReceivedDate, c.DeliveryDate, c.Status, c.Units, c.Total, c.IsInvoiced ? "نعم" : "", c.Notes
                }).ToList(),
                Footer = [null, $"{list.Count} حالة", null, null, null, null, null, null, list.Sum(c => c.Units), billable.Sum(c => c.Total), null, null],
                Note = "الإجمالي في السطر الأخير لا يشمل الحالات الملغاة."
            };

            var (content, type, name) = await export.ExportAsync(report, ExportService.ParseFormat(format));
            return File(content, type, name);
        }

        private async Task<List<LabCase>> FilterAsync(int? year, int? month, Guid? doctorId, Guid? clinicId, string? status, string? search)
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

            return await query
                .OrderByDescending(c => c.Year)
                .ThenByDescending(c => c.Month)
                .ThenByDescending(c => c.CaseNumber)
                .ToListAsync();
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
            NormalizeItems(model);
            ClearObsoleteModelState(model);
            var lines = model.Items.Where(i => !i.IsEmpty).ToList();

            if (lines.Count == 0)
                ModelState.AddModelError(string.Empty, "أضف سطراً واحداً على الأقل (المنتج والعدد والسعر).");
            ValidateTeeth(model);

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
                ToothPosition = SummarizeTeeth(lines),
                DocumentNumber = null,
                ReceiptNumber = null,
                Notes = model.Notes,
                Year = model.Year,
                Month = model.Month,
                Status = model.Status,
                Created = DateTime.UtcNow
            };

            await BuildItems(labCase, lines);

            _context.LabCases.Add(labCase);
            var creditCreated = await TryCreateClinicCreditAsync(labCase, model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = creditCreated
                ? $"تم إضافة الحالة وإنشاء رصيد دائن {model.ClinicCreditAmount ?? labCase.Total:N2}"
                : $"تم إضافة الحالة — الإجمالي {labCase.Total:N2}";
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
            return View(await ToFormAsync(labCase));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, LabCaseFormVM model)
        {
            if (id != model.Id)
                return View("NotFound");

            ApplyDates(model);
            NormalizeItems(model);
            ClearObsoleteModelState(model);
            var lines = model.Items.Where(i => !i.IsEmpty).ToList();

            if (lines.Count == 0)
                ModelState.AddModelError(string.Empty, "أضف سطراً واحداً على الأقل (المنتج والعدد والسعر).");
            ValidateTeeth(model);

            var existing = await _context.LabCases
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    c.Id,
                    c.InvoiceId,
                    InvoiceNumber = c.Invoice == null ? (int?)null : c.Invoice.Number
                })
                .FirstOrDefaultAsync();

            if (existing == null)
                return View("NotFound");

            if (!ModelState.IsValid)
            {
                await FillLookups();
                model.IsInvoiced = existing.InvoiceId.HasValue;
                model.InvoiceNumber = existing.InvoiceNumber;
                var existingCredit = await _context.ClinicCreditEntries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.LabCaseId == id);
                model.HasClinicCredit = existingCredit != null;
                model.ExistingClinicCreditAmount = existingCredit?.Amount ?? 0m;
                if (model.Items.Count == 0)
                    model.Items.Add(new LabCaseItemFormVM());
                return View(model);
            }

            var tooth = SummarizeTeeth(lines);
            var modified = DateTime.UtcNow;

            // لقطة قبل التعديل لسجل التغييرات (التعديل هنا يتم بأوامر جماعية لا يراها ChangeTracker).
            var before = await _context.LabCases
                .AsNoTracking()
                .Include(c => c.Doctor)
                .Include(c => c.Clinic)
                .Include(c => c.Items).ThenInclude(i => i.ServiceType)
                .FirstAsync(c => c.Id == id);

            await using var tx = await _context.Database.BeginTransactionAsync();

            var updated = await _context.LabCases
                .Where(c => c.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.CaseNumber, model.CaseNumber)
                    .SetProperty(c => c.PatientName, model.PatientName)
                    .SetProperty(c => c.DoctorId, model.DoctorId)
                    .SetProperty(c => c.ClinicId, model.ClinicId)
                    .SetProperty(c => c.ReceivedDate, model.ReceivedDate)
                    .SetProperty(c => c.DeliveryDate, model.DeliveryDate)
                    .SetProperty(c => c.ToothPosition, tooth)
                    .SetProperty(c => c.DocumentNumber, (string?)null)
                    .SetProperty(c => c.ReceiptNumber, (string?)null)
                    .SetProperty(c => c.Notes, model.Notes)
                    .SetProperty(c => c.Year, model.Year)
                    .SetProperty(c => c.Month, model.Month)
                    .SetProperty(c => c.Status, model.Status)
                    .SetProperty(c => c.Modified, modified));

            if (updated == 0)
            {
                await tx.RollbackAsync();
                return View("NotFound");
            }

            await _context.LabCaseItems
                .Where(i => i.LabCaseId == id)
                .ExecuteDeleteAsync();

            // Drop any leftover tracked graph so SaveChanges only INSERTs new lines.
            _context.ChangeTracker.Clear();

            var shell = new LabCase
            {
                Id = id,
                CaseNumber = model.CaseNumber,
                PatientName = model.PatientName,
                DoctorId = model.DoctorId,
                ClinicId = model.ClinicId,
                Status = model.Status
            };
            await BuildItems(shell, lines);

            foreach (var item in shell.Items)
                _context.LabCaseItems.Add(item);

            var creditCreated = await TryCreateClinicCreditAsync(shell, model);

            await AddEditAuditAsync(before, model, shell.Items);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            var total = shell.Items.Sum(i => i.LineTotal);
            TempData["SuccessMessage"] = creditCreated
                ? $"تم حفظ الحالة وإنشاء رصيد دائن {model.ClinicCreditAmount ?? total:N2}"
                : $"تم حفظ الحالة — الإجمالي {total:N2}";
            return RedirectToAction(nameof(Details), new { id });
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
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.LabCases.Remove(labCase);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الحالة";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>اقتراحات أسماء الحالات (المرضى) السابقة لحقل الاسم في نموذج الحالة.</summary>
        [HttpGet]
        public async Task<JsonResult> PatientNames(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(Array.Empty<string>());

            // المقارنة في الذاكرة بعد توحيد الحروف العربية (أ/إ/آ، ة/ه، ى/ي) — عدد الأسماء صغير.
            var names = await _context.LabCases
                .AsNoTracking()
                .Select(c => c.PatientName)
                .Distinct()
                .ToListAsync();

            var term = ArabicText.Normalize(q);
            var matches = names
                .Where(n => !string.IsNullOrWhiteSpace(n) && ArabicText.Normalize(n).Contains(term))
                .OrderBy(n => ArabicText.Normalize(n).StartsWith(term) ? 0 : 1)
                .ThenBy(n => n)
                .Take(20)
                .ToList();

            return Json(matches);
        }

        /// <summary>سعر نوع العمل لهذا الطبيب/العيادة + التكلفة.</summary>
        public async Task<JsonResult> ServicePrice(Guid id, Guid? doctorId, Guid? clinicId)
        {
            var result = await _pricing.ResolveAsync(id, doctorId, clinicId);
            var cost = await _context.ServiceTypes
                .Where(s => s.Id == id)
                .Select(s => s.UnitCost)
                .FirstOrDefaultAsync();

            return Json(new
            {
                price = result.Price,
                generalPrice = result.GeneralPrice,
                isDoctorPrice = result.IsDoctorPrice,
                isClinicPrice = result.IsClinicPrice,
                cost,
                name = result.ServiceName
            });
        }

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

            var clinicPrices = labCase.ClinicId.HasValue
                ? await _context.ClinicPrices
                    .AsNoTracking()
                    .Where(p => p.ClinicId == labCase.ClinicId.Value && serviceIds.Contains(p.ServiceTypeId))
                    .ToDictionaryAsync(p => p.ServiceTypeId, p => p.Price)
                : [];

            var remakeSequence = 0;
            var isFree = labCase.Status == CaseStatus.Free;

            foreach (var line in lines)
            {
                var service = services.FirstOrDefault(s => s.Id == line.ServiceTypeId);
                var treatAsRemake = line.IsRemake || labCase.Status == CaseStatus.Remake;

                decimal ResolveCatalogPrice()
                {
                    if (service == null) return 0m;
                    if (doctorPrices.TryGetValue(service.Id, out var doctorSpecial))
                        return doctorSpecial;
                    if (clinicPrices.TryGetValue(service.Id, out var clinicSpecial))
                        return clinicSpecial;
                    return service.UnitPrice;
                }

                var price = line.UnitPrice;
                var priced = false;   // سطر يأخذ سعراً (ليس مجانياً بالقاعدة)
                if (isFree)
                {
                    price = 0;
                }
                else if (treatAsRemake)
                {
                    // الإعادة: المستخدم يقرر — السعر كما كُتب (0 = مجانية للطبيب). لا قاعدة تلقائية.
                    remakeSequence++;
                }
                else
                {
                    priced = true;
                    if (price == 0 && service != null)
                        price = ResolveCatalogPrice();
                }

                // «سعر يدوي» = السعر يختلف عن قائمة الأسعار. إن لم يُغيَّر السعر في هذا التعديل نُبقي العلامة كما هي.
                var isManual = false;
                if (priced && service != null)
                {
                    isManual = line.OriginalUnitPrice.HasValue && line.OriginalUnitPrice.Value == price
                        ? line.IsManualPrice
                        : price != ResolveCatalogPrice();
                }

                var cost = line.UnitCost > 0
                    ? line.UnitCost
                    : service?.UnitCost ?? 0m;

                labCase.Items.Add(new LabCaseItem
                {
                    Id = Guid.NewGuid(),
                    LabCaseId = labCase.Id,
                    ServiceTypeId = line.ServiceTypeId,
                    ProductName = string.IsNullOrWhiteSpace(line.ProductName) ? service?.Name : line.ProductName,
                    Quantity = Math.Max(1, line.Quantity),
                    UnitPrice = price,
                    UnitCost = cost,
                    IsManualPrice = isManual,
                    IsRemake = treatAsRemake,
                    RemakeReason = treatAsRemake ? line.RemakeReason : null,
                    RemakeSequence = treatAsRemake ? remakeSequence : 0,
                    Teeth = string.IsNullOrWhiteSpace(line.Teeth) ? null : line.Teeth,
                    ToothPosition = Truncate(LineTeethText(line), 100),
                    Notes = line.Notes,
                    Created = DateTime.UtcNow
                });
            }
        }

        private async Task<LabCaseFormVM> ToFormAsync(LabCase labCase)
        {
            var credit = await _context.ClinicCreditEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.LabCaseId == labCase.Id);

            return new LabCaseFormVM
            {
                Id = labCase.Id,
                CaseNumber = labCase.CaseNumber,
                PatientName = labCase.PatientName,
                DoctorId = labCase.DoctorId,
                ClinicId = labCase.ClinicId,
                ReceivedDate = labCase.ReceivedDate,
                DeliveryDate = labCase.DeliveryDate,
                Notes = labCase.Notes,
                Year = labCase.Year,
                Month = labCase.Month,
                Status = labCase.Status,
                IsInvoiced = labCase.InvoiceId.HasValue,
                InvoiceNumber = labCase.Invoice?.Number,
                HasClinicCredit = credit != null,
                ExistingClinicCreditAmount = credit?.Amount ?? 0m,
                ClinicCreditAmount = credit?.Amount ?? labCase.Total,
                Items = labCase.Items
                    .OrderBy(i => i.IsRemake)
                    .ThenBy(i => i.Created)
                    .Select(i =>
                    {
                        var (arch, numbers) = ParseTooth(i.ToothPosition);
                        return new LabCaseItemFormVM
                        {
                            Id = i.Id,
                            ServiceTypeId = i.ServiceTypeId,
                            ProductName = i.ProductName,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            OriginalUnitPrice = i.UnitPrice,
                            IsManualPrice = i.IsManualPrice,
                            UnitCost = i.UnitCost,
                            IsRemake = i.IsRemake,
                            RemakeReason = i.RemakeReason,
                            ToothArch = arch,
                            ToothNumbers = numbers,
                            ToothPosition = i.ToothPosition,
                            Teeth = i.Teeth,
                            Notes = i.Notes
                        };
                    }).ToList()
            };
        }

        /// <summary>
        /// عند إلغاء حالة مدفوعة مسبقاً: إنشاء رصيد دائن للعيادة مرة واحدة لكل حالة.
        /// </summary>
        private async Task<bool> TryCreateClinicCreditAsync(LabCase labCase, LabCaseFormVM model)
        {
            if (!model.CreateClinicCredit)
                return false;

            if (labCase.Status != CaseStatus.Cancelled)
                return false;

            if (!labCase.ClinicId.HasValue)
            {
                TempData["ErrorMessage"] = "لا يمكن إنشاء رصيد دائن بدون تحديد العيادة.";
                return false;
            }

            var amount = model.ClinicCreditAmount ?? labCase.Total;
            if (amount <= 0)
                return false;

            var exists = await _context.ClinicCreditEntries
                .AnyAsync(c => c.LabCaseId == labCase.Id);

            if (exists)
                return false;

            _context.ClinicCreditEntries.Add(new ClinicCreditEntry
            {
                Id = Guid.NewGuid(),
                ClinicId = labCase.ClinicId.Value,
                LabCaseId = labCase.Id,
                Amount = amount,
                Date = DateTime.Now.Date,
                Notes = $"إلغاء حالة مدفوعة — رقم {labCase.CaseNumber} — {labCase.PatientName}",
                Created = DateTime.UtcNow
            });

            return true;
        }

        /// <summary>يسجّل في سجل التغييرات ما تغيّر في رأس الحالة وأسطرها.</summary>
        private async Task AddEditAuditAsync(LabCase before, LabCaseFormVM model, ICollection<LabCaseItem> newItems)
        {
            var changes = new List<string>();

            void Diff(string label, object? oldValue, object? newValue)
            {
                if (!Equals(oldValue, newValue))
                    changes.Add($"{label}: {AuditTrail.Format(oldValue)} ← {AuditTrail.Format(newValue)}");
            }

            async Task<string?> DoctorName(Guid? id) => id == null ? null
                : await _context.Doctors.AsNoTracking().Where(d => d.Id == id).Select(d => d.Name).FirstOrDefaultAsync();
            async Task<string?> ClinicName(Guid? id) => id == null ? null
                : await _context.Clinics.AsNoTracking().Where(c => c.Id == id).Select(c => c.Name).FirstOrDefaultAsync();

            Diff("رقم الحالة", before.CaseNumber, model.CaseNumber);
            Diff("اسم الحالة", before.PatientName, model.PatientName);
            if (before.DoctorId != model.DoctorId)
                Diff("الطبيب", before.Doctor?.Name, await DoctorName(model.DoctorId));
            if (before.ClinicId != model.ClinicId)
                Diff("المركز / العيادة", before.Clinic?.Name, await ClinicName(model.ClinicId));
            Diff("تاريخ الاستلام", before.ReceivedDate, model.ReceivedDate);
            Diff("تاريخ التسليم", before.DeliveryDate, model.DeliveryDate);
            Diff("الحالة", before.Status, model.Status);
            Diff("ملاحظات", string.IsNullOrWhiteSpace(before.Notes) ? null : before.Notes,
                             string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes);

            var oldItems = AuditTrail.DescribeItems(before.Items.OrderBy(i => i.IsRemake).ThenBy(i => i.Created));
            var newItemsText = AuditTrail.DescribeItems(newItems);
            if (oldItems != newItemsText)
            {
                changes.Add($"الأسطر قبل: {oldItems}");
                changes.Add($"الأسطر بعد: {newItemsText}");
                changes.Add($"الإجمالي: {before.Total:N2} ← {newItems.Sum(i => i.LineTotal):N2}");
            }

            if (changes.Count == 0)
                return;

            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserName = _context.CurrentUserName,
                Action = AuditAction.Modified,
                EntityType = nameof(LabCase),
                EntityLabel = AuditTrail.LabelFor(nameof(LabCase)),
                EntityId = before.Id.ToString(),
                Summary = $"حالة رقم {model.CaseNumber} — {model.PatientName}",
                Changes = string.Join("\n", changes)
            });
        }

        /// <summary>يصلح قيم العدد الفارغة قبل التحقق حتى لا يفشل التعديل بعد الحفظ.</summary>
        private static void NormalizeItems(LabCaseFormVM model)
        {
            foreach (var item in model.Items)
            {
                if (item.Quantity < 1 && !item.IsEmpty)
                    item.Quantity = 1;

                if (model.Status == CaseStatus.Remake && !item.IsEmpty)
                    item.IsRemake = true;
            }
        }

        private void ClearObsoleteModelState(LabCaseFormVM model)
        {
            foreach (var key in new[]
                     {
                         nameof(LabCaseFormVM.Year),
                         nameof(LabCaseFormVM.Month),
                         nameof(LabCaseFormVM.DocumentNumber),
                         nameof(LabCaseFormVM.ReceiptNumber),
                         nameof(LabCaseFormVM.ToothPosition)
                     })
            {
                ModelState.Remove(key);
            }

            for (var i = 0; i < model.Items.Count; i++)
            {
                if (!model.Items[i].IsEmpty)
                    continue;

                foreach (var field in new[] { "Quantity", "UnitPrice", "UnitCost", "ServiceTypeId", "ProductName" })
                    ModelState.Remove($"Items[{i}].{field}");
            }
        }

        private static void ApplyDates(LabCaseFormVM model)
        {
            var d = model.ReceivedDate ?? DateTime.Now;
            model.ReceivedDate ??= d.Date;
            model.Year = d.Year;
            model.Month = d.Month;
        }

        /// <summary>
        /// يتحقق من الأسنان المختارة لكل سطر (الأرقام الدولية، الجسور متجاورة في فك واحد، طرفا الجسر ليسا دمية)
        /// ويوحّد صيغتها. لا يُعتمد على تحقق المتصفح.
        /// </summary>
        private void ValidateTeeth(LabCaseFormVM model)
        {
            var lineNo = 0;
            foreach (var item in model.Items)
            {
                if (item.IsEmpty) continue;
                lineNo++;
                if (string.IsNullOrWhiteSpace(item.Teeth))
                {
                    item.Teeth = null;
                    continue;
                }

                var parsed = TeethCode.Parse(item.Teeth);
                if (!parsed.IsValid)
                    ModelState.AddModelError(string.Empty, $"السطر {lineNo} — الأسنان: " + string.Join(" ", parsed.Errors));
                else
                    item.Teeth = parsed.Count == 0 ? null : TeethCode.Serialize(parsed);
            }
        }

        /// <summary>نص الأسنان للسطر: من صليب الأسنان إن وُجد، وإلا النص القديم (حالات ما قبل الصليب).</summary>
        private static string? LineTeethText(LabCaseItemFormVM line)
        {
            if (!string.IsNullOrWhiteSpace(line.Teeth))
                return TeethCode.ToDisplay(line.Teeth);
            return FormatTooth(line.ToothArch, line.ToothNumbers)
                   ?? (string.IsNullOrWhiteSpace(line.ToothPosition) ? null : line.ToothPosition.Trim());
        }

        private static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..max];

        private static string? FormatTooth(string? arch, string? numbers)
        {
            arch = string.IsNullOrWhiteSpace(arch) ? null : arch.Trim();
            numbers = string.IsNullOrWhiteSpace(numbers) ? null : numbers.Trim();
            if (arch == null && numbers == null)
                return null;
            if (arch != null && numbers != null)
                return $"{arch}: {numbers}";
            return arch ?? numbers;
        }

        private static (string? Arch, string? Numbers) ParseTooth(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (null, null);

            foreach (var arch in ToothArch.All)
            {
                if (value.Equals(arch, StringComparison.Ordinal))
                    return (arch, null);
                var prefix = arch + ":";
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return (arch, value[prefix.Length..].Trim());
                if (value.StartsWith(arch, StringComparison.Ordinal))
                    return (arch, value[arch.Length..].Trim().TrimStart(':', '—', '-', ' '));
            }

            return (null, value.Trim());
        }

        private static string? SummarizeTeeth(List<LabCaseItemFormVM> lines)
        {
            var parts = lines
                .Select(LineTeethText)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();
            return parts.Count == 0 ? null : Truncate(string.Join(" | ", parts), 100);
        }

        private async Task FillLookups(int? year = null, int? month = null, Guid? doctorId = null, Guid? clinicId = null, string? status = null)
        {
            ViewBag.Doctors = new SelectList(await _context.Doctors.AsNoTracking().OrderBy(d => d.Name).ToListAsync(), "Id", "Name", doctorId);
            ViewBag.Clinics = new SelectList(await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "Id", "Name", clinicId);
            var services = await _context.ServiceTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            ViewBag.Services = new SelectList(services, "Id", "Name");
            ViewBag.ServiceList = services;
            ViewBag.Statuses = new SelectList(CaseStatus.All, status);
            ViewBag.StatusHelp = CaseStatus.All.ToDictionary(s => s, CaseStatus.Describe);
            ViewBag.RemakeReasons = new SelectList(RemakeReason.All);
            ViewBag.ToothArches = new SelectList(ToothArch.All);
            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.DoctorId = doctorId;
            ViewBag.ClinicId = clinicId;
            ViewBag.Status = status;
        }
    }
}
