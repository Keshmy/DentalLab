using DentalLab.Models;
using DentalLab.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// تسعير الأسطر:
    ///   السعر = سعر الطبيب إن وُجد، وإلا سعر العيادة، وإلا السعر العام.
    /// والإعادة: لا تسعير تلقائي — المستخدم يحدد في النموذج هل هي مجانية (0) أو بسعر.
    /// </summary>
    public class PricingService
    {
        private readonly AppDbContext _context;

        public PricingService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>السعر المطبَّق: طبيب ← عيادة ← عام.</summary>
        public async Task<PriceResult> ResolveAsync(Guid serviceTypeId, Guid? doctorId, Guid? clinicId = null)
        {
            var service = await _context.ServiceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serviceTypeId);

            if (service == null)
                return new PriceResult();

            var result = new PriceResult
            {
                ServiceTypeId = service.Id,
                ServiceName = service.Name,
                GeneralPrice = service.UnitPrice,
                Price = service.UnitPrice
            };

            if (clinicId.HasValue)
            {
                var clinicSpecial = await _context.ClinicPrices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId.Value && p.ServiceTypeId == serviceTypeId);

                if (clinicSpecial != null)
                {
                    result.Price = clinicSpecial.Price;
                    result.IsClinicPrice = true;
                }
            }

            if (doctorId.HasValue)
            {
                var doctorSpecial = await _context.DoctorPrices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.DoctorId == doctorId.Value && p.ServiceTypeId == serviceTypeId);

                if (doctorSpecial != null)
                {
                    result.Price = doctorSpecial.Price;
                    result.IsDoctorPrice = true;
                    result.IsClinicPrice = false;
                }
            }

            return result;
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

        public async Task<List<PriceResult>> ResolveAllForClinicAsync(Guid clinicId)
        {
            var services = await _context.ServiceTypes
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            var overrides = await _context.ClinicPrices
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId)
                .ToDictionaryAsync(p => p.ServiceTypeId, p => p.Price);

            return services.Select(s => new PriceResult
            {
                ServiceTypeId = s.Id,
                ServiceName = s.Name,
                GeneralPrice = s.UnitPrice,
                Price = overrides.TryGetValue(s.Id, out var special) ? special : s.UnitPrice,
                IsClinicPrice = overrides.ContainsKey(s.Id)
            }).ToList();
        }
    }

    public class PriceResult
    {
        public Guid ServiceTypeId { get; set; }
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>السعر العام في قائمة الأسعار.</summary>
        public decimal GeneralPrice { get; set; }

        /// <summary>السعر المطبَّق (الطبيب ← العيادة ← العام).</summary>
        public decimal Price { get; set; }

        public bool IsDoctorPrice { get; set; }

        public bool IsClinicPrice { get; set; }

        public decimal Difference => Price - GeneralPrice;
    }
}
