using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// مراجعة وتحديث أسعار الحالات غير المفوترة بعد تعديل قائمة الأسعار.
    /// يُفتح تلقائياً بعد حفظ سعر عام أو أسعار طبيب/عيادة إذا وُجدت أسطر تتأثر.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin,Employee")]
    public class RepriceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RepriceService _reprice;

        public RepriceController(AppDbContext context, RepriceService reprice)
        {
            _context = context;
            _reprice = reprice;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid? serviceTypeId, Guid? doctorId, Guid? clinicId, string? returnUrl)
        {
            var scope = new RepriceScope { ServiceTypeId = serviceTypeId, DoctorId = doctorId, ClinicId = clinicId };
            ViewBag.Scope = scope;
            ViewBag.ScopeTitle = await ScopeTitleAsync(scope);
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            var lines = await _reprice.PreviewAsync(scope);
            var previous = RepricePrevious.Decode(TempData[RepricePrevious.TempDataKey] as string);
            foreach (var l in lines)
                l.FollowedOldPrice = previous.TryGetValue(l.ServiceTypeId, out var prices) && prices.Contains(l.OldPrice);
            return View(lines);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(Guid? serviceTypeId, Guid? doctorId, Guid? clinicId, List<Guid>? itemIds, string? returnUrl)
        {
            var scope = new RepriceScope { ServiceTypeId = serviceTypeId, DoctorId = doctorId, ClinicId = clinicId };

            if (itemIds == null || itemIds.Count == 0)
            {
                TempData["ErrorMessage"] = "لم تختر أي سطر للتحديث.";
                return RedirectToAction(nameof(Index), new { serviceTypeId, doctorId, clinicId, returnUrl });
            }

            var result = await _reprice.ApplyAsync(scope, itemIds);

            TempData["SuccessMessage"] = result.Lines == 0
                ? "لا توجد أسطر تحتاج تحديثاً (ربما فُوترت أو عُدِّلت في هذه الأثناء)."
                : $"تم تحديث {result.Lines} سطر في {result.Cases} حالة — فرق الإجمالي {result.Difference:N2}.";

            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl!);
            return RedirectToAction(nameof(Index), new { serviceTypeId, doctorId, clinicId });
        }

        private async Task<string> ScopeTitleAsync(RepriceScope scope)
        {
            var parts = new List<string>();
            if (scope.ServiceTypeId.HasValue)
                parts.Add("المنتج: " + await _context.ServiceTypes.Where(s => s.Id == scope.ServiceTypeId).Select(s => s.Name).FirstOrDefaultAsync());
            if (scope.DoctorId.HasValue)
                parts.Add("الطبيب: " + await _context.Doctors.Where(d => d.Id == scope.DoctorId).Select(d => d.Name).FirstOrDefaultAsync());
            if (scope.ClinicId.HasValue)
                parts.Add("العيادة: " + await _context.Clinics.Where(c => c.Id == scope.ClinicId).Select(c => c.Name).FirstOrDefaultAsync());
            return parts.Count == 0 ? "كل الأسعار" : string.Join(" — ", parts);
        }
    }
}
