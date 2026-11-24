using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Services;
using DentalLab.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin,Employee")]
    public class AdminController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly FinanceService _finance;
        private readonly ExcelImportService _importService;
        private readonly BackupService _backup;

        public AdminController(
            AppDbContext context,
            FinanceService finance,
            ExcelImportService importService,
            BackupService backup,
            IWebHostEnvironment host) : base(host)
        {
            _context = context;
            _finance = finance;
            _importService = importService;
            _backup = backup;
        }

        [HttpGet]
        [Route("Dashboard")]
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var summary = await _finance.BuildSummaryAsync(now.Year, now.Month);
            var monthRow = summary.Months.FirstOrDefault();
            var accounts = await _finance.GetAccountBalancesAsync();
            var receivables = await _finance.GetClinicReceivablesAsync();

            var vm = new DashboardVM
            {
                Year = now.Year,
                Month = now.Month,
                TotalCases = await _context.LabCases.CountAsync(),
                CasesThisMonth = await _context.LabCases.CountAsync(c => c.Year == now.Year && c.Month == now.Month),
                PendingDelivery = await _context.LabCases.CountAsync(c => c.DeliveredAt == null && c.Status != CaseStatus.Cancelled
                                                                      && c.DeliveryDate != null && c.DeliveryDate <= now.Date),
                DoctorsCount = await _context.Doctors.CountAsync(),
                ClinicsCount = await _context.Clinics.CountAsync(),

                MonthUnits = monthRow?.Units ?? 0,
                MonthRemakeUnits = monthRow?.RemakeUnits ?? 0,
                MonthRevenue = monthRow?.Revenue ?? 0m,
                MonthInvoiced = await _context.LabCaseItems
                    .Where(i => i.LabCase!.Year == now.Year && i.LabCase.Month == now.Month
                                && i.LabCase.Status != CaseStatus.Cancelled && i.LabCase.InvoiceId != null)
                    .SumAsync(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0m,
                MonthCollections = monthRow?.Collections ?? 0m,
                MonthExpenses = monthRow?.OperatingExpenses ?? 0m,
                MonthNetProfit = monthRow?.NetProfit ?? 0m,
                MonthFreeRemakeCost = monthRow?.FreeRemakeCost ?? 0m,

                TotalReceivables = summary.TotalReceivables,
                TotalLoanBalance = summary.TotalLoanBalance,
                CashBalance = accounts.Sum(a => a.Balance),
                Accounts = accounts,
                TopReceivables = receivables.Take(6).ToList(),

                RecentCases = await _context.LabCases
                    .AsNoTracking()
                    .Include(c => c.Doctor)
                    .Include(c => c.Clinic)
                    .Include(c => c.Items)
                    .OrderByDescending(c => c.ReceivedDate)
                    .ThenByDescending(c => c.CaseNumber)
                    .Take(8)
                    .ToListAsync()
            };

            return View(vm);
        }

        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> SiteDetails()
        {
            var site = await _context.SiteInfo.FirstOrDefaultAsync();
            return site == null ? View("NotFound") : View(site);
        }

        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> SiteEdit()
        {
            var site = await _context.SiteInfo.FirstOrDefaultAsync();
            return site == null ? View("NotFound") : View(site);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> SiteEdit(SiteInfo model)
        {
            var site = await _context.SiteInfo.FirstOrDefaultAsync();
            if (site == null)
                return View("NotFound");

            site.Name = model.Name;
            site.Activity = model.Activity;
            site.About = model.About;
            site.Phone = model.Phone;
            site.BankAccountName = model.BankAccountName;
            site.BankName = model.BankName;
            site.BankAccountNumber = model.BankAccountNumber;
            site.PartnerName = model.PartnerName;
            site.PartnerSharePercent = model.PartnerSharePercent;
            site.Modified = DateTime.UtcNow;

            if (model.Logo != null && CheckImgExtension(model.Logo))
                site.LogoUrl = UploadFile("site", model.Logo, site.LogoUrl, "keep");

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حفظ بيانات المختبر";
            return RedirectToAction(nameof(SiteDetails));
        }

        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> Backup()
        {
            var settings = await _backup.GetOrCreateSettingsAsync();
            var ensure = _backup.EnsureFolder(settings.FolderPath);
            var vm = new BackupPageVM
            {
                FolderPath = settings.FolderPath,
                AutoEnabled = settings.AutoEnabled,
                AutoHour = settings.AutoHour,
                AutoMinute = settings.AutoMinute,
                LastBackupAt = settings.LastBackupAt,
                LastBackupMessage = settings.LastBackupMessage,
                LastAutoBackupDate = settings.LastAutoBackupDate,
                SuggestedFolders = _backup.GetSuggestedFolders(),
                ExistingBackups = _backup.ListBackups(settings.FolderPath),
                FolderReady = ensure.Ok,
                FolderError = ensure.Error
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> BackupSettings(BackupPageVM model)
        {
            if (!ModelState.IsValid)
            {
                model.SuggestedFolders = _backup.GetSuggestedFolders();
                model.ExistingBackups = _backup.ListBackups(model.FolderPath);
                return View("Backup", model);
            }

            await _backup.SaveSettingsAsync(model.FolderPath, model.AutoEnabled, model.AutoHour, model.AutoMinute);
            TempData["SuccessMessage"] = "تم حفظ إعدادات النسخ الاحتياطي";
            return RedirectToAction(nameof(Backup));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> RunBackup()
        {
            var result = await _backup.CreateBackupAsync(isAuto: false);
            TempData[result.Ok ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Backup));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> RestoreBackup(string path)
        {
            var result = await _backup.RestoreAsync(path);
            TempData[result.Ok ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Backup));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        [RequestSizeLimit(512_000_000)]
        public async Task<IActionResult> RestoreUpload(IFormFile backupFile)
        {
            var result = await _backup.RestoreFromUploadAsync(backupFile);
            TempData[result.Ok ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Backup));
        }

        /// <summary>
        /// الاستيراد القديم (ملف أرشيف بتنسيق قديم) استُبدل بصفحة «استيراد الحالات» ذات القالب والمعاينة والتراجع.
        /// الروابط القديمة تُحوَّل إليها.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public IActionResult ImportArchive() => RedirectToAction("Index", "CaseImport");

        /// <summary>
        /// الاستيراد يكون فقط من ملف يختاره المستخدم بنفسه — لا يُستورد ملف الأرشيف المرفق مع التطبيق
        /// (كان ذلك يضيف بيانات تجريبية إلى قاعدة بيانات العميل بضغطة واحدة).
        /// </summary>
        private static string? ValidateExcelUpload(IFormFile? file, bool confirm)
        {
            if (file == null || file.Length == 0)
                return "اختر ملف Excel أولاً.";
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return "الملف يجب أن يكون بصيغة Excel (.xlsx).";
            if (!confirm)
                return "ضع علامة التأكيد قبل بدء الاستيراد.";
            return null;
        }

        /// <summary>يقرأ الملف المرفوع في الذاكرة ويستورده؛ الملف التالف يعيد تنبيهاً بدل صفحة خطأ.</summary>
        private static async Task<ImportResultVM> ImportUploadAsync(IFormFile file, Func<Stream, Task<ImportResultVM>> import)
        {
            using var memory = new MemoryStream();
            await file.CopyToAsync(memory);
            memory.Position = 0;

            try
            {
                return await import(memory);
            }
            catch (Exception ex) when (ex is ClosedXML.Excel.Exceptions.ClosedXMLException or InvalidDataException or System.IO.IOException or FormatException)
            {
                var result = new ImportResultVM();
                result.Warnings.Add("تعذّرت قراءة الملف — تأكد أنه ملف Excel صحيح وغير محمي بكلمة مرور. " + ex.Message);
                return result;
            }
        }

        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public IActionResult ImportFinance() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> ImportFinance(IFormFile? file, bool confirm)
        {
            var error = ValidateExcelUpload(file, confirm);
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                return View();
            }

            var result = await ImportUploadAsync(file!, stream => _importService.ImportFinanceAsync(stream));
            TempData["SuccessMessage"] =
                $"تم استيراد الماليات: {result.IncomeImported} إيراد ({result.IncomeTotal:N2})، " +
                $"{result.ExpensesImported} مصروف ({result.ExpensesTotal:N2})، {result.LoansImported} سلفة.";
            return View("ImportResult", result);
        }

        public async Task<JsonResult> CasesMonthChart()
        {
            var data = await _context.LabCases
                .AsNoTracking()
                .GroupBy(c => c.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(data.OrderBy(x => x.Month).ToDictionary(x => x.Month.ToString(), x => x.Count));
        }

        public async Task<JsonResult> CasesClinicChart()
        {
            var data = await _context.LabCases
                .AsNoTracking()
                .GroupBy(c => c.Clinic != null ? c.Clinic.Name : "بدون عيادة")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(data.ToDictionary(x => x.Name ?? "بدون", x => x.Count));
        }
    }
}
