using ClosedXML.Excel;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DentalLab.Services
{
    public class ExcelImportService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _host;

        /// <summary>علامة توضع على كل سطر ينشئه استيراد الإيرادات والمصاريف ليُحذف عند إعادة الاستيراد دون المساس بالإدخال اليدوي.</summary>
        private const string FinanceImportMarker = "[استيراد]";

        private const string FinanceUnconfirmedNote = "مكتوب في الملف بعلامة (/ أو \\) — لم يُؤكَّد استلامه/دفعه";
        private const string FinanceDollarNote = "المبلغ بالدولار في الملف — يحتاج مراجعة سعر الصرف";

        public ExcelImportService(AppDbContext context, IWebHostEnvironment host)
        {
            _context = context;
            _host = host;
        }

        public async Task<ImportResultVM> ImportAsync(string? filePath = null)
        {
            var result = new ImportResultVM();
            filePath ??= Path.Combine(_host.ContentRootPath, "Data", "Archive.xlsx");

            if (!File.Exists(filePath))
            {
                result.Warnings.Add("ملف الأرشيف غير موجود: " + filePath);
                return result;
            }

            using var workbook = new XLWorkbook(filePath);

            // كل مرحلة تُحفظ قبل التالية: المراحل اللاحقة تقرأ الأطباء والمراكز وقائمة الأسعار
            // من قاعدة البيانات، فلو لم تُحفظ لأُنشئت من جديد (تكرار) ولفشلت مطابقة الأسعار.
            await ImportPrices(workbook, result);
            await _context.SaveChangesAsync();

            await ImportCases(workbook, " 2025", 2025, result);
            await _context.SaveChangesAsync();

            await ImportCases(workbook, " 2026", 2026, result);
            await _context.SaveChangesAsync();

            return result;
        }

        /// <summary>
        /// استيراد ملف الإيرادات والمصاريف (Data\Finance.xlsx) إلى جداول المصاريف والإيرادات والسلف.
        /// كل ورقة تمثل سنة، وتحتوي أعمدة المصاريف على اليمين وأعمدة الإيرادات على اليسار.
        /// الاستيراد قابل للإعادة: تُحذف أولاً الأسطر التي تحمل علامة الاستيراد.
        /// </summary>
        public async Task<ImportResultVM> ImportFinanceAsync(string? filePath = null)
        {
            var result = new ImportResultVM();
            filePath ??= Path.Combine(_host.ContentRootPath, "Data", "Finance.xlsx");

            if (!File.Exists(filePath))
            {
                result.Warnings.Add("ملف الإيرادات والمصاريف غير موجود: " + filePath);
                return result;
            }

            await RemoveFinanceImportedRows();

            var context = new FinanceContext
            {
                Result = result,
                Categories = await _context.ExpenseCategories.ToListAsync(),
                Accounts = await _context.FinancialAccounts.ToListAsync(),
                Clinics = await _context.Clinics.ToListAsync()
            };

            using var workbook = new XLWorkbook(filePath);

            // المجاميع المكتوبة يدوياً في الملف نفسه، تُستخدم للمطابقة وإظهار الفروقات
            ImportFinanceSheet(workbook, "2025", 2025, FinanceLayout2025, 17040m, 16830m, context);
            ImportFinanceSheet(workbook, "2026", 2026, FinanceLayout2026, 52840m, 43045m, context);

            AddFinanceReviewNotes(context);

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task ImportPrices(XLWorkbook workbook, ImportResultVM result)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Trim().Equals("Prices", StringComparison.OrdinalIgnoreCase));
            if (sheet == null)
            {
                result.Warnings.Add("ورقة الأسعار غير موجودة");
                return;
            }

            var existing = await _context.ServiceTypes.ToListAsync();

            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                var name = CellText(row.Cell(3));
                var priceCell = row.Cell(4);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                name = name.Trim();
                if (existing.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    result.PricesSkipped++;
                    continue;
                }

                decimal price = 0;
                if (priceCell.TryGetValue(out double d))
                    price = (decimal)d;
                else
                    decimal.TryParse(CellText(priceCell), NumberStyles.Any, CultureInfo.InvariantCulture, out price);

                var service = new ServiceType
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    UnitPrice = price,
                    Created = DateTime.UtcNow
                };
                _context.ServiceTypes.Add(service);
                existing.Add(service);
                result.PricesImported++;
            }
        }

        private async Task ImportCases(XLWorkbook workbook, string sheetName, int defaultYear, ImportResultVM result)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name == sheetName)
                        ?? workbook.Worksheets.FirstOrDefault(s => s.Name.Trim() == sheetName.Trim());
            if (sheet == null)
            {
                result.Warnings.Add("الورقة غير موجودة: " + sheetName);
                return;
            }

            var doctors = await _context.Doctors.ToListAsync();
            var clinics = await _context.Clinics.ToListAsync();
            var services = await _context.ServiceTypes.ToListAsync();
            var existingKeys = (await _context.LabCases
                    .Where(c => c.CaseNumber != null)
                    .Select(c => new { c.CaseNumber, c.Year })
                    .ToListAsync())
                .Select(c => (c.CaseNumber!.Value, c.Year))
                .ToHashSet();

            var headerRow = sheet.RowsUsed().FirstOrDefault(r => CellText(r.Cell(1)).Contains("رقم الحالة"));
            if (headerRow == null)
            {
                result.Warnings.Add("لم يتم العثور على عناوين الأعمدة في " + sheetName);
                return;
            }

            // الحالات المضافة في هذه الجلسة، لربط الأسطر المكرَّرة (نفس رقم الحالة) بنفس الحالة
            var pending = new Dictionary<(int, int), LabCase>();

            var map = BuildHeaderMap(headerRow);
            int startRow = headerRow.RowNumber() + 1;
            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? startRow;

            for (int r = startRow; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var patientName = GetMapped(row, map, "اسم الحالة");
                var caseNoRaw = GetMapped(row, map, "رقم الحالة");
                if (string.IsNullOrWhiteSpace(patientName) && string.IsNullOrWhiteSpace(caseNoRaw))
                    continue;

                int? caseNumber = ParseInt(caseNoRaw);
                var date = ParseDate(GetMapped(row, map, "تاريخ الاستلام"));
                int year = date?.Year ?? defaultYear;
                int month = ParseMonth(GetMapped(row, map, "شهر"), date, year);

                string? doctorName = GetMapped(row, map, "الطبيب");
                string? clinicName = GetMapped(row, map, "العيادة");
                string? workType = GetMapped(row, map, "نوع العمل");
                string? notes = GetMapped(row, map, "ملاحظات");
                string? teeth = GetMapped(row, map, "مكان الاسنان") ?? GetMapped(row, map, "مكان الأسنان");
                int quantity = ParseInt(GetMapped(row, map, "العدد")) ?? 0;
                var (price, priceNote, status) = ParsePrice(GetMapped(row, map, "السعر"));

                Guid? doctorId = null;
                if (!string.IsNullOrWhiteSpace(doctorName))
                {
                    var doctor = doctors.FirstOrDefault(d => d.Name == doctorName.Trim());
                    if (doctor == null)
                    {
                        doctor = new Doctor { Id = Guid.NewGuid(), Name = doctorName.Trim(), Created = DateTime.UtcNow };
                        _context.Doctors.Add(doctor);
                        doctors.Add(doctor);
                        result.DoctorsCreated++;
                    }
                    doctorId = doctor.Id;
                }

                Guid? clinicId = null;
                if (!string.IsNullOrWhiteSpace(clinicName))
                {
                    var clinic = clinics.FirstOrDefault(c => c.Name == clinicName.Trim());
                    if (clinic == null)
                    {
                        clinic = new Clinic { Id = Guid.NewGuid(), Name = clinicName.Trim(), Created = DateTime.UtcNow };
                        _context.Clinics.Add(clinic);
                        clinics.Add(clinic);
                        result.ClinicsCreated++;
                    }
                    clinicId = clinic.Id;
                }

                Guid? serviceId = MatchService(workType, services);
                var service = serviceId.HasValue ? services.FirstOrDefault(s => s.Id == serviceId.Value) : null;
                if (status == CaseStatus.Normal && !string.IsNullOrWhiteSpace(notes))
                {
                    if (notes.Contains("ملغي"))
                        status = CaseStatus.Cancelled;
                    else if (notes.Contains("مجاني"))
                        status = CaseStatus.Free;
                    else if (notes.Contains("اعاد", StringComparison.OrdinalIgnoreCase) || notes.Contains("Remake", StringComparison.OrdinalIgnoreCase))
                        status = CaseStatus.Remake;
                    else if (notes.Contains("مدفوع"))
                        status = CaseStatus.Paid;
                }

                if (!string.IsNullOrWhiteSpace(priceNote))
                    notes = string.IsNullOrWhiteSpace(notes) ? priceNote : notes + " | " + priceNote;

                // سعر الوحدة: عمود "السعر في الفاتورة" في الأرشيف هو إجمالي السطر، فنقسمه على العدد.
                // وإن كان العمود فارغاً (كما في ورقة 2026 بالكامل) نأخذ السعر من قائمة الأسعار.
                var qty = quantity <= 0 ? 1 : quantity;
                var priced = status != CaseStatus.Free && status != CaseStatus.Cancelled;
                decimal unitPrice;

                if (price.HasValue)
                {
                    unitPrice = Math.Round(price.Value / qty, 2, MidpointRounding.AwayFromZero);
                    if (unitPrice * qty != price.Value)
                        result.RoundingAdjustments++;
                }
                else if (priced && service != null)
                {
                    unitPrice = service.UnitPrice;
                    result.ItemsPricedFromList++;
                }
                else
                {
                    unitPrice = 0m;
                    if (priced)
                        result.ItemsWithoutPrice++;
                }

                var item = new LabCaseItem
                {
                    ServiceTypeId = serviceId,
                    ProductName = workType?.Trim(),
                    Quantity = qty,
                    UnitPrice = unitPrice,
                    UnitCost = service?.UnitCost ?? 0m,
                    ToothPosition = teeth?.Trim(),
                    Created = DateTime.UtcNow
                };

                result.ItemsImported++;

                // رقم الحالة المكرَّر في الأرشيف يعني عملاً إضافياً لنفس الحالة، فيُضاف كسطر جديد لا كحالة جديدة.
                if (caseNumber.HasValue && pending.TryGetValue((caseNumber.Value, year), out var openCase))
                {
                    openCase.Items.Add(item);
                    result.ItemsMergedIntoExistingCase++;
                    continue;
                }

                if (caseNumber.HasValue && existingKeys.Contains((caseNumber.Value, year)))
                {
                    result.CasesSkipped++;
                    continue;
                }

                var labCase = new LabCase
                {
                    CaseNumber = caseNumber,
                    PatientName = string.IsNullOrWhiteSpace(patientName) ? ("حالة " + (caseNumber?.ToString() ?? r.ToString())) : patientName.Trim(),
                    DoctorId = doctorId,
                    ClinicId = clinicId,
                    ReceivedDate = date,
                    ToothPosition = teeth?.Trim(),
                    Notes = notes?.Trim(),
                    Year = year,
                    Month = month,
                    Status = status,
                    Created = DateTime.UtcNow
                };

                labCase.Items.Add(item);

                _context.LabCases.Add(labCase);
                if (caseNumber.HasValue)
                    pending[(caseNumber.Value, year)] = labCase;
                result.CasesImported++;
            }
        }

        private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.CellsUsed())
            {
                var text = CellText(cell);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                map[text.Trim()] = cell.Address.ColumnNumber;
            }
            return map;
        }

        private static string? GetMapped(IXLRow row, Dictionary<string, int> map, string keyPart)
        {
            var match = map.FirstOrDefault(kv => kv.Key.Contains(keyPart, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null)
                return null;
            return CellText(row.Cell(match.Value));
        }

        private static string CellText(IXLCell cell)
        {
            if (cell.IsEmpty())
                return string.Empty;
            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime().ToString("yyyy-M-d", CultureInfo.InvariantCulture);
            if (cell.DataType == XLDataType.Number)
                return cell.GetDouble().ToString(CultureInfo.InvariantCulture);
            return cell.GetString().Trim();
        }

        private static int? ParseInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var digits = Regex.Match(value, @"\d+");
            if (digits.Success && int.TryParse(digits.Value, out int n))
                return n;
            return null;
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (value.Contains('X', StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(value, @"\d{1,2}"))
                return null;

            var cleaned = value.Replace(" ", "");
            cleaned = Regex.Replace(cleaned, @"[./]", "-");
            string[] formats = ["d-M-yyyy", "dd-MM-yyyy", "yyyy-M-d", "yyyy-MM-dd", "d-M-yy"];
            if (DateTime.TryParseExact(cleaned, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(value, new CultureInfo("ar-LY"), DateTimeStyles.None, out dt))
                return dt;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;
            return null;
        }

        private static int ParseMonth(string? monthText, DateTime? date, int year)
        {
            if (date.HasValue)
                return date.Value.Month;
            if (!string.IsNullOrWhiteSpace(monthText))
            {
                var m = Regex.Match(monthText, @"(\d{1,2})");
                if (m.Success && int.TryParse(m.Value, out int month) && month is >= 1 and <= 12)
                    return month;
            }
            return 1;
        }

        private static (decimal? price, string? note, string status) ParsePrice(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (null, null, CaseStatus.Normal);

            if (value.Contains("ملغي"))
                return (null, value, CaseStatus.Cancelled);
            if (value.Contains("مجاني"))
                return (0, value, CaseStatus.Free);
            if (value.Contains("ترحيل"))
                return (null, value, CaseStatus.Normal);

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            {
                var status = p == 0 ? CaseStatus.Free : CaseStatus.Normal;
                return (p, null, status);
            }

            return (null, value, CaseStatus.Normal);
        }

        private static Guid? MatchService(string? workType, List<ServiceType> services)
        {
            if (string.IsNullOrWhiteSpace(workType))
                return null;

            var cleaned = workType.Replace("\n", " ").Trim();
            var exact = services.FirstOrDefault(s => string.Equals(s.Name, cleaned, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact.Id;

            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["P.F.M"] = "Porcalin",
                ["PFM"] = "Porcalin",
                ["Porcalin (PFM)"] = "Porcalin",
                ["مؤقت"] = "TEMPORARY CAD-CAM",
                ["تبييض منزلي"] = "BLEACHING",
                ["واقي ليلي"] = "NIGHT GUARD",
                ["ZIRCON OVER IMPLANT"] = "ZIRCONIA OVER IMPLANT",
                ["Screw retained"] = "SCREW RETAINED ZIRCONIA",
                ["ZIRCONIA FULL ANATOMY - عالي الصلابة"] = "ZIRCONIA FULL ANATOMY",
                ["ZIRCONIA FULL ANATOMY - عالي الشفافية"] = "High Translucency Zirconia"
            };

            foreach (var alias in aliases)
            {
                if (cleaned.Contains(alias.Key, StringComparison.OrdinalIgnoreCase))
                {
                    var match = services.FirstOrDefault(s => s.Name.Contains(alias.Value, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        return match.Id;
                }
            }

            return services.FirstOrDefault(s =>
                cleaned.Contains(s.Name, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(cleaned, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        /// <summary>حذف كل ما سبق استيراده من ملف الإيرادات والمصاريف، مع ترك الأسطر المُدخلة يدوياً كما هي.</summary>
        private async Task RemoveFinanceImportedRows()
        {
            _context.Expenses.RemoveRange(await _context.Expenses
                .Where(e => e.Notes != null && e.Notes.Contains(FinanceImportMarker))
                .ToListAsync());

            _context.IncomeEntries.RemoveRange(await _context.IncomeEntries
                .Where(i => i.Notes != null && i.Notes.Contains(FinanceImportMarker))
                .ToListAsync());

            _context.LoanTransactions.RemoveRange(await _context.LoanTransactions
                .Where(l => l.Description != null && l.Description.Contains(FinanceImportMarker))
                .ToListAsync());
        }

        private void ImportFinanceSheet(XLWorkbook workbook, string sheetName, int year, FinanceLayout layout,
            decimal fileIncome, decimal fileExpenses, FinanceContext context)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Trim() == sheetName);
            if (sheet == null)
            {
                context.Result.Warnings.Add("ورقة السنة غير موجودة في ملف الإيرادات والمصاريف: " + sheetName);
                return;
            }

            decimal expensesTotal = 0m;
            decimal incomeTotal = 0m;
            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

            // الصف الأول عناوين، والبيانات تبدأ من الصف الثاني
            for (int r = 2; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var expense = ParseFinanceAmount(row.Cell(layout.ExpenseAmount));
                var income = ParseFinanceAmount(row.Cell(layout.IncomeAmount));

                if (expense.amount <= 0 && income.amount <= 0)
                    continue;

                int? month = ParseFinanceMonth(FinanceCellText(row, layout.MonthText), FinanceCellText(row, layout.PeriodNote));
                if (month == null)
                    context.RowsWithoutMonth++;

                var date = new DateTime(year, month ?? 1, 1);

                expensesTotal += ImportFinanceExpense(row, r, year, month ?? 1, date, sheetName, layout, expense, context);
                incomeTotal += ImportFinanceIncome(row, r, year, month ?? 1, date, sheetName, layout, income, context);
            }

            AddFinanceReconciliation(sheetName, incomeTotal, expensesTotal, fileIncome, fileExpenses, context.Result);
        }

        /// <summary>يستورد سطر مصروف واحد ويعيد المبلغ المضاف للمصاريف (صفر إذا تم تجاهله أو سُجّل كسلفة).</summary>
        private decimal ImportFinanceExpense(IXLRow row, int rowNumber, int year, int month, DateTime date, string sheetName,
            FinanceLayout layout, (decimal amount, bool isConfirmed, string? note) parsed, FinanceContext context)
        {
            if (parsed.amount <= 0)
                return 0m;

            var description = FinanceCellText(row, layout.ExpenseDescription).Trim();

            // أسطر المجاميع في الملف ليست حركات
            if (description.Contains("مجموع"))
                return 0m;

            if (string.IsNullOrWhiteSpace(description))
            {
                context.RowsWithoutDescription++;
                context.Result.Warnings.Add(
                    $"تحقق من مبلغ {parsed.amount:0.##} بدون بيان (ورقة {sheetName} صف {rowNumber}) — لم يُستورد كمصروف. "
                    + "إن كان سداد دين علينا فسجّله في شاشة السلف بحركة (سداد دين علينا)، وإن كان مجموعاً فاتركه.");
                return 0m;
            }

            var rowNote = BuildFinanceRowNote(parsed, "مصروف", sheetName, rowNumber, context);

            // السلفة ليست مصروفاً: تُسجَّل ذمة على الشخص ولا تنقص الربح
            if (TryGetFinanceLoanPerson(description, out string personName))
            {
                _context.LoanTransactions.Add(new LoanTransaction
                {
                    Id = Guid.NewGuid(),
                    Date = date,
                    Year = year,
                    Month = month,
                    PersonName = TruncateText(personName, 150),
                    Amount = parsed.amount,
                    Direction = LoanDirection.Given,
                    Description = AppendImportMarker(description + " — " + rowNote, 500),
                    IsWrittenOff = false,
                    Created = DateTime.UtcNow
                });

                context.LoanRows++;
                context.Result.LoansImported++;
                context.Result.LoansTotal += parsed.amount;
                return 0m;
            }

            var categoryName = MatchFinanceCategory(description);
            if (categoryName == ExpenseCategoryNames.Equipment)
                context.CapitalRows++;

            _context.Expenses.Add(new Expense
            {
                Id = Guid.NewGuid(),
                Date = date,
                Year = year,
                Month = month,
                Description = TruncateText(description, 300),
                Amount = parsed.amount,
                ExpenseCategoryId = FinanceCategoryId(categoryName, context),
                IsPaid = parsed.isConfirmed,
                Notes = AppendImportMarker(rowNote, 500),
                Created = DateTime.UtcNow
            });

            context.Result.ExpensesImported++;
            context.Result.ExpensesTotal += parsed.amount;
            return parsed.amount;
        }

        /// <summary>يستورد سطر إيراد واحد ويعيد المبلغ المضاف للإيرادات (صفر إذا تم تجاهله).</summary>
        private decimal ImportFinanceIncome(IXLRow row, int rowNumber, int year, int month, DateTime date, string sheetName,
            FinanceLayout layout, (decimal amount, bool isConfirmed, string? note) parsed, FinanceContext context)
        {
            if (parsed.amount <= 0)
                return 0m;

            var description = FinanceCellText(row, layout.IncomeDescription).Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                context.RowsWithoutDescription++;
                return 0m;
            }

            var accountText = FinanceCellText(row, layout.IncomeAccount).Trim();
            var methodText = layout.IncomeMethod > 0 ? FinanceCellText(row, layout.IncomeMethod).Trim() : accountText;
            var rowNote = BuildFinanceRowNote(parsed, "إيراد", sheetName, rowNumber, context);

            // ورقة 2026 فيها عمود "تم الاستلام"، أما 2025 فتُعتبر مستلمة ما لم تكن مؤشرة بعلامة
            bool isReceived = parsed.isConfirmed
                && (layout.IncomeStatus <= 0 || FinanceCellText(row, layout.IncomeStatus).Contains("تم الاستلام"));

            _context.IncomeEntries.Add(new IncomeEntry
            {
                Id = Guid.NewGuid(),
                Date = date,
                Year = year,
                Month = month,
                Description = TruncateText(description, 300),
                Amount = parsed.amount,
                ClinicId = MatchFinanceClinic(description, context.Clinics),
                FinancialAccountId = MatchFinanceAccount(accountText, context.Accounts),
                Method = MatchFinanceMethod(methodText),
                IsReceived = isReceived,
                Notes = AppendImportMarker(rowNote, 500),
                Created = DateTime.UtcNow
            });

            context.Result.IncomeImported++;
            context.Result.IncomeTotal += parsed.amount;
            return parsed.amount;
        }

        /// <summary>
        /// خلية المبلغ قد تكون رقماً أو نصاً: علامة (/ أو \) تعني أن المبلغ لم يُؤكَّد (ومجاميع الملف نفسها تستثنيه)،
        /// وعلامة ($) تعني أن المبلغ بالدولار.
        /// </summary>
        private static (decimal amount, bool isConfirmed, string? note) ParseFinanceAmount(IXLCell cell)
        {
            if (cell.IsEmpty())
                return (0m, false, null);

            if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double numeric))
                return ((decimal)Math.Abs(numeric), true, null);

            var raw = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(raw) || !Regex.IsMatch(raw, @"\d"))
                return (0m, false, null);

            var digits = Regex.Replace(raw, @"[^\d.]", string.Empty);
            if (!decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value) || value <= 0)
                return (0m, false, null);

            if (raw.Contains('/') || raw.Contains('\\'))
                return (value, false, FinanceUnconfirmedNote);

            if (raw.Contains('$'))
                return (value, false, FinanceDollarNote);

            return (value, true, null);
        }

        /// <summary>أول رقم بين 1 و12 في نص الشهر، وإلا في نص الفترة.</summary>
        private static int? ParseFinanceMonth(params string?[] texts)
        {
            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                foreach (Match match in Regex.Matches(text, @"\d{1,2}"))
                {
                    if (int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int month)
                        && month is >= 1 and <= 12)
                    {
                        return month;
                    }
                }
            }

            return null;
        }

        /// <summary>يسجّل ملاحظات السطر (غير مؤكد / بالدولار) ويعيد النص الذي يُحفظ مع الحركة.</summary>
        private static string BuildFinanceRowNote((decimal amount, bool isConfirmed, string? note) parsed,
            string label, string sheetName, int rowNumber, FinanceContext context)
        {
            if (!parsed.isConfirmed)
                context.UnconfirmedRows++;

            if (parsed.note == FinanceDollarNote)
                context.Result.Warnings.Add($"تحقق من {label} بمبلغ {parsed.amount:0.##} (ورقة {sheetName} صف {rowNumber}): {FinanceDollarNote}.");

            var text = $"ورقة {sheetName} صف {rowNumber}";
            return parsed.note == null ? text : text + " — " + parsed.note;
        }

        /// <summary>تصنيف المصروف بالكلمات المفتاحية، والبنود الرأسمالية أولاً حتى لا تُحسب الأجهزة ضمن المواد.</summary>
        private static string MatchFinanceCategory(string description)
        {
            bool Has(params string[] keywords)
                => keywords.Any(k => description.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (Has("لابتوب", "كمبيوتر", "ssd", "راوتر", "ميموري", "كرت شاشه", "كرت شاشة", "رامات", "يو بي اس",
                    "UP3D", "جهاز", "ماكينة", "سنترفيوج", "سبيكرا", "بطاريات", "فرازة", "بلندر"))
                return ExpenseCategoryNames.Equipment;
            if (Has("نسبة"))
                return ExpenseCategoryNames.PartnerShare;
            if (Has("مكافأة", "مكافاة"))
                return ExpenseCategoryNames.Salaries;
            if (Has("ايجار", "إيجار"))
                return ExpenseCategoryNames.Rent;
            if (Has("عمولة", "sms"))
                return ExpenseCategoryNames.BankFees;
            if (Has("بايونير"))
                return ExpenseCategoryNames.OutsourcedLab;
            if (Has("كرت نت", "كروت نت", "كرت شحن", "نت"))
                return ExpenseCategoryNames.Utilities;
            if (Has("نقل", "شحن"))
                return ExpenseCategoryNames.Transport;
            if (Has("صيانة", "قطع غيار", "فلتر", "كشف", "صمامات", "حوض", "ملحقات", "مستلزمات كهربائية", "مواد كهربائية"))
                return ExpenseCategoryNames.Maintenance;
            if (Has("بلكات", "مواد", "مستلزمات", "رنق", "اكياس", "أكياس", "مطهر", "كحول", "تنظيف", "بيرات", "moldastone", "splint"))
                return ExpenseCategoryNames.Materials;

            return ExpenseCategoryNames.Other;
        }

        private static Guid? FinanceCategoryId(string name, FinanceContext context)
        {
            var category = context.Categories.FirstOrDefault(c => string.Equals(c.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (category != null)
                return category.Id;

            var warning = "بند المصروف غير موجود في قاعدة البيانات: " + name;
            if (!context.Result.Warnings.Contains(warning))
                context.Result.Warnings.Add(warning);
            return null;
        }

        /// <summary>مطابقة الجهة الدافعة بعيادة مسجلة فقط؛ لا تُنشأ عيادات جديدة من هذا الملف.</summary>
        private static Guid? MatchFinanceClinic(string description, List<Clinic> clinics)
        {
            var token = StripClinicPrefix(description);
            if (token.Length < 3)
                return null;

            foreach (var clinic in clinics)
            {
                var key = StripClinicPrefix(clinic.Name);
                if (key.Length < 3)
                    continue;

                if (token.Contains(key, StringComparison.OrdinalIgnoreCase)
                    || key.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return clinic.Id;
                }
            }

            return null;
        }

        private static string StripClinicPrefix(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var cleaned = text.Replace("مركز", " ").Replace("عيادة", " ").Replace("عياده", " ");
            return Regex.Replace(cleaned, @"\s+", " ").Trim();
        }

        private static Guid? MatchFinanceAccount(string? accountText, List<FinancialAccount> accounts)
        {
            if (string.IsNullOrWhiteSpace(accountText))
                return null;

            string keyword;
            if (accountText.Contains("عبدو"))
                keyword = "عبدو";
            else if (accountText.Contains("عادل"))
                keyword = "عادل";
            else
                return null;

            return accounts.FirstOrDefault(a => a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        private static string? MatchFinanceMethod(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (text.Contains("حوالة"))
                return PaymentMethod.Transfer;
            if (text.Contains("كاش"))
                return PaymentMethod.Cash;
            return null;
        }

        /// <summary>يتحقق إن كان البيان سلفة/ديناً ويستخرج اسم الشخص بعد كلمة الدلالة.</summary>
        private static bool TryGetFinanceLoanPerson(string description, out string personName)
        {
            personName = string.Empty;
            if (string.IsNullOrWhiteSpace(description))
                return false;

            string[] keywords = ["دين على", "سلفة", "سلف "];
            foreach (var keyword in keywords)
            {
                int index = description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    continue;

                var rest = description[(index + keyword.Length)..].Trim();
                personName = string.IsNullOrWhiteSpace(rest) ? description.Trim() : rest;
                return true;
            }

            return false;
        }

        private static void AddFinanceReconciliation(string sheetName, decimal income, decimal expenses,
            decimal fileIncome, decimal fileExpenses, ImportResultVM result)
        {
            result.Warnings.Add($"تحقق من إيرادات {sheetName}: المستورد {income:0.##} مقابل {fileIncome:0.##} المكتوب في الملف (الفرق {income - fileIncome:0.##}).");
            result.Warnings.Add($"تحقق من مصاريف {sheetName}: المستورد {expenses:0.##} مقابل {fileExpenses:0.##} المكتوب في الملف (الفرق {expenses - fileExpenses:0.##}).");
            result.Warnings.Add($"تحقق من صافي {sheetName}: المحسوب {income - expenses:0.##} مقابل {fileIncome - fileExpenses:0.##} في الملف (الفرق {income - expenses - (fileIncome - fileExpenses):0.##}).");
        }

        private static void AddFinanceReviewNotes(FinanceContext context)
        {
            if (context.RowsWithoutMonth > 0)
                context.Result.Warnings.Add($"تحقق من {context.RowsWithoutMonth} سطراً لم يُذكر فيها الشهر — تم اعتماد الشهر (1) مؤقتاً.");

            context.Result.Warnings.Add(
                $"تحقق من ملخص الاستيراد: {context.UnconfirmedRows} سطراً مكتوباً بعلامة (/ أو \\) استُورد كغير مؤكد، "
                + $"و{context.RowsWithoutDescription} سطراً تم تجاهله لعدم وجود بيان، "
                + $"و{context.LoanRows} سطراً سُجّل كسلفة بدل مصروف، "
                + $"و{context.CapitalRows} سطراً صُنّف كأجهزة ومعدات (بند رأسمالي).");
        }

        private static string FinanceCellText(IXLRow row, int column)
            => column <= 0 ? string.Empty : CellText(row.Cell(column));

        private static string AppendImportMarker(string? text, int maxLength)
        {
            var body = (text ?? string.Empty).Trim();
            int room = maxLength - FinanceImportMarker.Length - 1;
            if (body.Length > room)
                body = body[..room];
            return body.Length == 0 ? FinanceImportMarker : body + " " + FinanceImportMarker;
        }

        private static string TruncateText(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength];

        /// <summary>مواضع الأعمدة (1-based) في ورقة السنة؛ الصفر يعني أن العمود غير موجود في تلك الورقة.</summary>
        private sealed class FinanceLayout
        {
            public int ExpenseAmount { get; init; }
            public int ExpenseDescription { get; init; }
            public int PeriodNote { get; init; }
            public int IncomeAmount { get; init; }
            public int IncomeMethod { get; init; }
            public int IncomeDescription { get; init; }
            public int IncomeAccount { get; init; }
            public int IncomeStatus { get; init; }
            public int MonthText { get; init; }
        }

        private static readonly FinanceLayout FinanceLayout2025 = new()
        {
            ExpenseAmount = 1,      // A
            ExpenseDescription = 2, // B
            PeriodNote = 3,         // C (نص الفترة، يُستخدم للشهر عند غيابه)
            IncomeAmount = 5,       // E
            IncomeMethod = 6,       // F (كاش / حوالة)
            IncomeDescription = 7,  // G
            MonthText = 8           // H
        };

        private static readonly FinanceLayout FinanceLayout2026 = new()
        {
            ExpenseAmount = 1,      // A
            ExpenseDescription = 2, // B
            IncomeAmount = 5,       // E
            IncomeDescription = 6,  // F
            IncomeAccount = 7,      // G (حساب عبدو / حساب عادل)
            IncomeStatus = 8,       // H (تم الاستلام)
            MonthText = 10          // J
        };

        private sealed class FinanceContext
        {
            public required ImportResultVM Result { get; init; }
            public required List<ExpenseCategory> Categories { get; init; }
            public required List<FinancialAccount> Accounts { get; init; }
            public required List<Clinic> Clinics { get; init; }

            public int UnconfirmedRows { get; set; }
            public int RowsWithoutDescription { get; set; }
            public int RowsWithoutMonth { get; set; }
            public int LoanRows { get; set; }
            public int CapitalRows { get; set; }
        }
    }
}
