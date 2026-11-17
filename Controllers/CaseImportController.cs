using System.Security.Cryptography;
using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>
    /// استيراد الحالات من قالب Excel: رفع ← معاينة (أخطاء + أسماء غير مسجلة) ← تأكيد. مع سجل الاستيرادات والتراجع.
    /// </summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class CaseImportController : Controller
    {
        private const long MaxFileBytes = 5_000_000;

        private readonly CaseImportService _import;
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;

        public CaseImportController(CaseImportService import, AppDbContext context, IDataProtectionProvider protection)
        {
            _import = import;
            _context = context;
            // الملف ينتقل بين صفحة المعاينة والتأكيد مشفّراً داخل النموذج، فلا يُخزَّن على الخادم.
            _protector = protection.CreateProtector("DentalLab.CaseImport.v1");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var batches = await _context.ImportBatches.AsNoTracking()
                .OrderByDescending(b => b.Created)
                .Take(50)
                .ToListAsync();
            return View(batches);
        }

        [HttpGet]
        public async Task<IActionResult> Template()
        {
            var bytes = await _import.BuildTemplateAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "قالب-استيراد-الحالات.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileBytes + 100_000)]
        public async Task<IActionResult> Preview(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BackWithError("اختر ملف Excel أولاً.");
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BackWithError("الملف يجب أن يكون بصيغة Excel (.xlsx). استخدم القالب الذي تنزّله من هذه الصفحة.");
            if (file.Length > MaxFileBytes)
                return BackWithError("الملف أكبر من 5 ميغابايت — قسّمه إلى أكثر من ملف.");

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            var preview = await _import.PreviewAsync(new MemoryStream(bytes), file.FileName);
            if (preview.FileErrors.Count > 0)
                return BackWithError(string.Join(" ", preview.FileErrors));

            ViewBag.Payload = _protector.Protect(Convert.ToBase64String(bytes));
            return View(preview);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileBytes * 2)]
        [RequestFormLimits(ValueLengthLimit = (int)(MaxFileBytes * 2))]
        public async Task<IActionResult> Confirm(string payload, string fileName, List<NameChoice> choices)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(_protector.Unprotect(payload));
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentNullException)
            {
                return BackWithError("انتهت صلاحية صفحة المعاينة (ربما أُعيد تشغيل الخادم) — ارفع الملف مرة أخرى.");
            }

            var result = await _import.CommitAsync(new MemoryStream(bytes), fileName, choices ?? [], User.Identity?.Name);
            if (!result.Succeeded)
                return BackWithError(string.Join(" ", result.Errors));

            var b = result.Batch!;
            TempData["SuccessMessage"] =
                $"تم استيراد {b.CaseCount} حالة ({b.ItemCount} سطر) من «{b.FileName}». " +
                $"أطباء جدد: {b.DoctorsCreated}، عيادات جديدة: {b.ClinicsCreated}، منتجات جديدة: {b.ProductsCreated}." +
                (result.SkippedCases > 0 ? $" تُخطّيت {result.SkippedCases} حالة بسبب أخطاء." : "");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Undo(Guid id)
        {
            var result = await _import.UndoAsync(id, User.Identity?.Name);
            if (result.Error != null)
                TempData["ErrorMessage"] = result.Error;
            else
                TempData["SuccessMessage"] =
                    $"تم التراجع: حُذفت {result.CasesDeleted} حالة ({result.ItemsDeleted} سطر)، " +
                    $"و{result.DoctorsDeleted} طبيب و{result.ClinicsDeleted} عيادة و{result.ProductsDeleted} منتج." +
                    (result.Kept > 0 ? $" أُبقي على {result.Kept} اسم لأنه استُخدم بعد الاستيراد." : "");
            return RedirectToAction(nameof(Index));
        }

        private IActionResult BackWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction(nameof(Index));
        }
    }
}
