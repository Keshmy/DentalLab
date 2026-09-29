using DentalLab.Models;
using DentalLab.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// تسعير الأسطر:
    ///   price(doctor, service) = السعر الخاص بالطبيب إن وُجد، وإلا السعر العام.
    /// والإعادة: الأولى على المعمل (مجاناً)، والثانية وما بعدها على الطبيب.
    /// </summary>
    public class PricingService
    {
        private readonly AppDbContext _context;

        public PricingService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>السعر المطبَّق فعلاً لطبيب معيّن على نوع عمل معيّن.</summary>
        public async Task<PriceResult> ResolveAsync(Guid serviceTypeId, Guid? doctorId)
        {
            var service = await _context.ServiceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serviceTypeId);

            if (service == null)
                return new PriceResult();

            var result = new PriceResult
            {
                ServiceName = service.Name,
                GeneralPrice = service.UnitPrice,
                Price = service.UnitPrice
            };

            if (doctorId.HasValue)
            {
                var special = await _context.DoctorPrices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.DoctorId == doctorId.Value && p.ServiceTypeId == serviceTypeId);

                if (special != null)
                {
                    result.Price = special.Price;
                    result.IsDoctorPrice = true;
                }
            }

            return result;
        }

        /// <summary>
        /// سعر الإعادة المقترح. القاعدة المتفق عليها:
        /// الإعادة الأولى لهذه الحالة على المعمل (صفر)، والإعادة الثانية وما بعدها على الطبيب.
        /// وخطأ المعمل مجاني دائماً بصرف النظر عن الترتيب.
        /// </summary>
        public async Task<RemakeResult> ResolveRemakeAsync(Guid caseId, Guid serviceTypeId, Guid? doctorId, string? reason)
        {
            var previousRemakes = await _context.LabCaseItems
                .AsNoTracking()
                .CountAsync(i => i.LabCaseId == caseId && i.IsRemake);

            var sequence = previousRemakes + 1;
            var price = await ResolveAsync(serviceTypeId, doctorId);

            var isLabFault = reason == RemakeReason.LabFault;
            var chargeable = sequence >= 2 && !isLabFault;

            return new RemakeResult
            {
                Sequence = sequence,
                IsChargeable = chargeable,
                Price = chargeable ? price.Price : 0m,
                FullPrice = price.Price,
                Explanation = isLabFault
                    ? "خطأ المعمل — مجاناً"
                    : sequence == 1
                        ? "الإعادة الأولى على المعمل — مجاناً"
                        : $"الإعادة رقم {sequence} — على الطبيب"
            };
        }

        /// <summary>كل الأسعار المطبَّقة لطبيب واحد (للعرض في شاشة أسعار الطبيب).</summary>
        public async Task<List<PriceResult>> ResolveAllForDoctorAsync(Guid doctorId)
        {
            var services = await _context.ServiceTypes
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            var overrides = await _context.DoctorPrices
                .AsNoTracking()
                .Where(p => p.DoctorId == doctorId)
                .ToDictionaryAsync(p => p.ServiceTypeId, p => p.Price);

            return services.Select(s => new PriceResult
            {
                ServiceTypeId = s.Id,
                ServiceName = s.Name,
                GeneralPrice = s.UnitPrice,
                Price = overrides.TryGetValue(s.Id, out var special) ? special : s.UnitPrice,
                IsDoctorPrice = overrides.ContainsKey(s.Id)
            }).ToList();
        }
    }

    public class PriceResult
    {
        public Guid ServiceTypeId { get; set; }
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>السعر العام في قائمة الأسعار.</summary>
        public decimal GeneralPrice { get; set; }

        /// <summary>السعر المطبَّق (الخاص إن وُجد، وإلا العام).</summary>
        public decimal Price { get; set; }

        public bool IsDoctorPrice { get; set; }

        public decimal Difference => Price - GeneralPrice;
    }

    public class RemakeResult
    {
        public int Sequence { get; set; }
        public bool IsChargeable { get; set; }
        public decimal Price { get; set; }
        public decimal FullPrice { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }
}
