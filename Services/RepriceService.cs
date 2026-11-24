using DentalLab.Models;
using DentalLab.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// تحديث أسعار الحالات غير المفوترة بعد تعديل قائمة الأسعار.
    ///
    /// يُحدَّث السطر فقط إذا:
    ///   • حالته غير مفوترة، وليست ملغاة ولا مجانية ولا مدفوعة،
    ///   • له نوع عمل من القائمة، وليس «سعراً يدوياً» (خصم/اتفاق خاص)،
    ///   • ليس سطر إعادة (سعر الإعادة يقرره المستخدم: مجانية أو بسعر).
    /// السعر الجديد بنفس ترتيب نموذج الحالة: الطبيب ← العيادة ← العام.
    /// لا يتغير شيء قبل أن يراجع المستخدم القائمة ويؤكد.
    /// </summary>
    public class RepriceService
    {
        private readonly AppDbContext _context;

        public RepriceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<RepriceLine>> PreviewAsync(RepriceScope scope)
        {
            var query = _context.LabCaseItems
                .AsNoTracking()
                .Where(i => i.LabCase!.InvoiceId == null
                            && i.LabCase.Status != CaseStatus.Cancelled
                            && i.LabCase.Status != CaseStatus.Free
                            && i.LabCase.Status != CaseStatus.Paid
                            && i.ServiceTypeId != null
                            && !i.IsManualPrice
                            && !i.IsRemake);

            if (scope.ServiceTypeId.HasValue)
                query = query.Where(i => i.ServiceTypeId == scope.ServiceTypeId);
            if (scope.DoctorId.HasValue)
                query = query.Where(i => i.LabCase!.DoctorId == scope.DoctorId);
            if (scope.ClinicId.HasValue)
                query = query.Where(i => i.LabCase!.ClinicId == scope.ClinicId);

            var items = await query
                .Select(i => new
                {
                    i.Id,
                    i.LabCaseId,
                    ServiceTypeId = i.ServiceTypeId!.Value,
                    i.ProductName,
                    i.IsRemake,
                    i.Quantity,
                    i.UnitPrice,
                    i.LabCase!.CaseNumber,
                    i.LabCase.PatientName,
                    i.LabCase.ReceivedDate,
                    i.LabCase.DoctorId,
                    i.LabCase.ClinicId,
                    DoctorName = i.LabCase.Doctor == null ? null : i.LabCase.Doctor.Name,
                    ClinicName = i.LabCase.Clinic == null ? null : i.LabCase.Clinic.Name
                })
                .ToListAsync();

            if (items.Count == 0)
                return [];

            var serviceIds = items.Select(i => i.ServiceTypeId).Distinct().ToList();
            var doctorIds = items.Where(i => i.DoctorId.HasValue).Select(i => i.DoctorId!.Value).Distinct().ToList();
            var clinicIds = items.Where(i => i.ClinicId.HasValue).Select(i => i.ClinicId!.Value).Distinct().ToList();

            var general = await _context.ServiceTypes.AsNoTracking()
                .Where(s => serviceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.UnitPrice);
            var doctorPrices = (await _context.DoctorPrices.AsNoTracking()
                    .Where(p => doctorIds.Contains(p.DoctorId) && serviceIds.Contains(p.ServiceTypeId))
                    .Select(p => new { p.DoctorId, p.ServiceTypeId, p.Price })
                    .ToListAsync())
                .ToDictionary(p => (p.DoctorId, p.ServiceTypeId), p => p.Price);
            var clinicPrices = (await _context.ClinicPrices.AsNoTracking()
                    .Where(p => clinicIds.Contains(p.ClinicId) && serviceIds.Contains(p.ServiceTypeId))
                    .Select(p => new { p.ClinicId, p.ServiceTypeId, p.Price })
                    .ToListAsync())
                .ToDictionary(p => (p.ClinicId, p.ServiceTypeId), p => p.Price);

            decimal Resolve(Guid serviceId, Guid? doctorId, Guid? clinicId)
            {
                if (doctorId.HasValue && doctorPrices.TryGetValue((doctorId.Value, serviceId), out var d)) return d;
                if (clinicId.HasValue && clinicPrices.TryGetValue((clinicId.Value, serviceId), out var c)) return c;
                return general.TryGetValue(serviceId, out var g) ? g : 0m;
            }

            return items
                .Select(i => new RepriceLine
                {
                    ItemId = i.Id,
                    CaseId = i.LabCaseId,
                    ServiceTypeId = i.ServiceTypeId,
                    CaseNumber = i.CaseNumber,
                    PatientName = i.PatientName,
                    ReceivedDate = i.ReceivedDate,
                    DoctorName = i.DoctorName,
                    ClinicName = i.ClinicName,
                    ProductName = i.IsRemake ? $"إعادة - {i.ProductName}" : i.ProductName,
                    Quantity = i.Quantity,
                    OldPrice = i.UnitPrice,
                    NewPrice = Resolve(i.ServiceTypeId, i.DoctorId, i.ClinicId)
                })
                // سعر 0 في القائمة يعني غالباً منتجاً بلا سعر — لا نصفّر به سطراً مسعّراً
                .Where(l => l.NewPrice != l.OldPrice && l.NewPrice > 0)
                .OrderBy(l => l.ReceivedDate)
                .ThenBy(l => l.CaseNumber)
                .ToList();
        }

        /// <summary>
        /// يطبّق السعر الجديد على الأسطر المختارة فقط، بعد إعادة الحساب
        /// (سطر فُوتر أو تغيّر منذ عرض القائمة لا يُلمس). يكتب سطراً في سجل التغييرات لكل حالة.
        /// </summary>
        public async Task<RepriceResult> ApplyAsync(RepriceScope scope, IReadOnlyCollection<Guid> selectedItemIds)
        {
            var selected = selectedItemIds.ToHashSet();
            var lines = (await PreviewAsync(scope)).Where(l => selected.Contains(l.ItemId)).ToList();
            if (lines.Count == 0)
                return new RepriceResult();

            var ids = lines.Select(l => l.ItemId).ToList();
            var items = await _context.LabCaseItems.Where(i => ids.Contains(i.Id)).ToListAsync();

            foreach (var item in items)
                item.UnitPrice = lines.First(l => l.ItemId == item.Id).NewPrice;

            foreach (var group in lines.GroupBy(l => l.CaseId))
            {
                var first = group.First();
                var changes = group
                    .Select(l => $"{l.ProductName} ×{l.Quantity}: {l.OldPrice:N2} ← {l.NewPrice:N2}")
                    .Append($"فرق الإجمالي: {group.Sum(l => l.Difference):N2}");

                _context.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    UserName = _context.CurrentUserName,
                    Action = AuditAction.Modified,
                    EntityType = nameof(LabCase),
                    EntityLabel = AuditTrail.LabelFor(nameof(LabCase)),
                    EntityId = first.CaseId.ToString(),
                    Summary = $"حالة رقم {first.CaseNumber} — {first.PatientName} (تحديث السعر من قائمة الأسعار)",
                    Changes = string.Join("\n", changes)
                });
            }

            await _context.SaveChangesAsync();

            return new RepriceResult
            {
                Lines = lines.Count,
                Cases = lines.Select(l => l.CaseId).Distinct().Count(),
                Difference = lines.Sum(l => l.Difference)
            };
        }
    }

    /// <summary>الأسعار التي كانت في القائمة قبل الحفظ، لكل نوع عمل تغيّر — تُمرَّر لصفحة المراجعة عبر TempData.</summary>
    public static class RepricePrevious
    {
        public const string TempDataKey = "RepricePrevious";

        public static string Encode(Dictionary<Guid, HashSet<decimal>> previous) =>
            string.Join(";", previous.Select(kv => kv.Key + "=" + string.Join("|",
                kv.Value.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)))));

        public static Dictionary<Guid, HashSet<decimal>> Decode(string? text)
        {
            var result = new Dictionary<Guid, HashSet<decimal>>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length != 2 || !Guid.TryParse(kv[0], out var id)) continue;
                result[id] = kv[1].Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => decimal.TryParse(v, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null)
                    .Where(d => d.HasValue).Select(d => d!.Value).ToHashSet();
            }
            return result;
        }
    }

    /// <summary>نطاق التحديث: نوع عمل، أو طبيب، أو عيادة (أو أكثر من واحد).</summary>
    public class RepriceScope
    {
        public Guid? ServiceTypeId { get; set; }
        public Guid? DoctorId { get; set; }
        public Guid? ClinicId { get; set; }
    }

    public class RepriceLine
    {
        public Guid ItemId { get; set; }
        public Guid CaseId { get; set; }
        public Guid ServiceTypeId { get; set; }

        /// <summary>
        /// السعر الحالي يساوي السعر الذي استُبدل للتو في قائمة الأسعار — أي أن السطر كان يتبع القائمة،
        /// فيُحدَّد افتراضياً. غير ذلك قد يكون سعراً متفقاً عليه فيُترك غير محدد.
        /// </summary>
        public bool FollowedOldPrice { get; set; }
        public int? CaseNumber { get; set; }
        public string? PatientName { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string? DoctorName { get; set; }
        public string? ClinicName { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public decimal Difference => (NewPrice - OldPrice) * Quantity;
    }

    public class RepriceResult
    {
        public int Lines { get; set; }
        public int Cases { get; set; }
        public decimal Difference { get; set; }
    }
}
