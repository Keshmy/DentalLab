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
                PendingDelivery = await _context.LabCases.CountAsync(c => c.DeliveryDate == null && c.Status != CaseStatus.Cancelled),
                DoctorsCount = await _context.Doctors.CountAsync(),
                ClinicsCount = await _context.Clinics.CountAsync(),

                MonthUnits = monthRow?.Units ?? 0,
                MonthRemakeUnits = monthRow?.RemakeUnits ?? 0,
                MonthRevenue = monthRow?.Revenue ?? 0m,
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

        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public IActionResult ImportArchive() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> ImportArchive(bool confirm)
        {
            var result = await _importService.ImportAsync();
            TempData["SuccessMessage"] =
                $"تم الاستيراد: {result.CasesImported} حالة، {result.ItemsImported} سطر، " +
                $"{result.PricesImported} سعر، {result.DoctorsCreated} طبيب، {result.ClinicsCreated} عيادة.";
            return View("ImportResult", result);
        }

        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public IActionResult ImportFinance() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> ImportFinance(bool confirm)
        {
            var result = await _importService.ImportFinanceAsync();
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
