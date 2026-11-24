using System.Globalization;
using ClosedXML.Excel;
using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// استيراد الحالات من قالب Excel الخاص بالبرنامج:
    ///   1) تنزيل القالب (فيه قوائم الأطباء/العيادات/المنتجات الحالية وورقة تعليمات).
    ///   2) رفع الملف ← معاينة: أخطاء كل صف + الأسماء غير المسجلة مع اقتراح أقرب اسم موجود.
    ///   3) التأكيد ← حفظ كل شيء دفعة واحدة (الكل أو لا شيء) مع رقم عملية استيراد يسمح بالتراجع.
    /// </summary>
    public class CaseImportService
    {
        public const int MaxRows = 5000;
        private const string CasesSheetName = "الحالات";

        private readonly AppDbContext _context;

        public CaseImportService(AppDbContext context)
        {
            _context = context;
        }

        // ───────────────────────── الأعمدة ─────────────────────────

        /// <summary>أعمدة القالب بالترتيب. «*» في القالب = مطلوب. الأسماء البديلة مقبولة عند القراءة.</summary>
        public static readonly ImportColumn[] Columns =
        [
            new("CaseNumber", "رقم الحالة", false, 12, "اتركه فارغاً ليُعطى الرقم التالي تلقائياً. الأسطر التي لها نفس الرقم = حالة واحدة بعدة منتجات."),
            new("Patient", "اسم الحالة", true, 24, "اسم المريض.", "اسم المريض"),
            new("Doctor", "الطبيب", true, 22, "اختر من القائمة أو اكتب اسماً جديداً."),
            new("Clinic", "العيادة", true, 22, "اختر من القائمة أو اكتب اسماً جديداً.", "المركز", "المركز / العيادة"),
            new("Received", "تاريخ الاستلام", true, 15, "مثال: 2026/11/17. يحدد شهر وسنة الحالة."),
            new("Due", "موعد التسليم", false, 15, "اختياري. إذا كان اليوم أو قبله تُعتبر الحالة مُسلَّمة."),
            new("Product", "المنتج", true, 26, "اختر من القائمة أو اكتب اسماً جديداً.", "نوع العمل"),
            new("Quantity", "العدد", false, 8, "رقم صحيح. فارغ = 1."),
            new("Price", "السعر", false, 10, "سعر الوحدة. فارغ = السعر من قائمة الأسعار (العيادة ← الطبيب ← العام)."),
            new("Teeth", "الأسنان", false, 18, "ترقيم دولي (FDI): 11 21 36. الجسر: 13-14p-15 (p = دمية). اللبنية: 51–85. المنتج المسعّر بالسن يأخذ عدده من الأسنان إذا تُرك «العدد» فارغاً.", "أرقام الأسنان", "مكان الأسنان"),
            new("Remake", "إعادة", false, 8, "نعم إذا كان السطر إعادة عمل."),
            new("RemakeReason", "سبب الإعادة", false, 16, "خطأ المعمل = مجانية دائماً."),
            new("Status", "الحالة", false, 10, "عادي / مدفوع / مجاني / ملغي / إعادة. فارغ = عادي.", "نوع الحالة"),
            new("Notes", "ملاحظات", false, 28, "اختياري.")
        ];

        private static string HeaderKey(string? text) =>
            ArabicText.Normalize((text ?? string.Empty).Replace("*", "").Replace(" / ", "/").Replace("/ ", "/").Replace(" /", "/"));

        // ───────────────────────── القالب ─────────────────────────

        public async Task<byte[]> BuildTemplateAsync()
        {
            var doctors = await _context.Doctors.AsNoTracking().OrderBy(d => d.Name).Select(d => d.Name).ToListAsync();
            var clinics = await _context.Clinics.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
            var products = await _context.ServiceTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => s.Name).ToListAsync();

            using var wb = new XLWorkbook();
            wb.RightToLeft = true;

            // 1) ورقة الحالات — فارغة تماماً تحت العناوين (لا أمثلة هنا حتى لا تُستورد بالخطأ).
            var ws = wb.Worksheets.Add(CasesSheetName);
            ws.RightToLeft = true;
            for (var i = 0; i < Columns.Length; i++)
            {
                var col = Columns[i];
                var cell = ws.Cell(1, i + 1);
                cell.Value = col.Required ? col.Title + " *" : col.Title;
                cell.Style.Font.SetBold();
                cell.Style.Fill.SetBackgroundColor(col.Required ? XLColor.FromHtml("#FDE2B8") : XLColor.FromHtml("#E9ECEF"));
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                cell.CreateComment().AddText(col.Help);
                ws.Column(i + 1).Width = col.Width;
            }
            ws.SheetView.FreezeRows(1);
            ws.Row(1).Height = 22;

            const int lastRow = MaxRows + 1;
            int ColOf(string key) => Array.FindIndex(Columns, c => c.Key == key) + 1;
            IXLRange Range(string key) => ws.Range(2, ColOf(key), lastRow, ColOf(key));

            Range("Received").Style.DateFormat.Format = "yyyy/mm/dd";
            Range("Due").Style.DateFormat.Format = "yyyy/mm/dd";
            Range("Price").Style.NumberFormat.Format = "#,##0.00";

            // 2) ورقة القوائم — مصدر القوائم المنسدلة.
            var lists = wb.Worksheets.Add("القوائم");
            lists.RightToLeft = true;
            IXLRange WriteList(int column, string title, IReadOnlyList<string> values)
            {
                lists.Cell(1, column).Value = title;
                lists.Cell(1, column).Style.Font.SetBold();
                for (var i = 0; i < values.Count; i++)
                    lists.Cell(i + 2, column).Value = values[i];
                lists.Column(column).Width = 28;
                return lists.Range(2, column, Math.Max(2, values.Count + 1), column);
            }

            var doctorsRef = WriteList(1, "الأطباء", doctors);
            var clinicsRef = WriteList(2, "العيادات", clinics);
            var productsRef = WriteList(3, "المنتجات", products);
            var statusRef = WriteList(4, "الحالة", CaseStatus.All);
            var reasonRef = WriteList(5, "سبب الإعادة", RemakeReason.All);
            var yesRef = WriteList(6, "إعادة", ["نعم", "لا"]);

            // قوائم الأسماء تسمح بكتابة اسم جديد (بدون رسالة خطأ)، وقوائم القيم الثابتة تمنع غيرها.
            void ListValidation(string key, IXLRange source, bool strict)
            {
                var v = Range(key).CreateDataValidation();
                v.List(source, true);
                v.IgnoreBlanks = true;
                v.ShowErrorMessage = strict;
                if (strict)
                {
                    v.ErrorTitle = "قيمة غير صحيحة";
                    v.ErrorMessage = "اختر قيمة من القائمة.";
                }
            }

            if (doctors.Count > 0) ListValidation("Doctor", doctorsRef, false);
            if (clinics.Count > 0) ListValidation("Clinic", clinicsRef, false);
            if (products.Count > 0) ListValidation("Product", productsRef, false);
            ListValidation("Status", statusRef, true);
            ListValidation("RemakeReason", reasonRef, true);
            ListValidation("Remake", yesRef, true);

            var qty = Range("Quantity").CreateDataValidation();
            qty.WholeNumber.Between(1, 10000);
            qty.IgnoreBlanks = true;
            qty.ErrorMessage = "العدد رقم صحيح من 1 فأكثر.";

            // 3) ورقة التعليمات مع مثال.
            var help = wb.Worksheets.Add("تعليمات");
            help.RightToLeft = true;
            var lines = new[]
            {
                "طريقة تعبئة قالب استيراد الحالات",
                "",
                "• املأ ورقة «الحالات» فقط. كل سطر = منتج واحد في حالة.",
                "• الأعمدة الملوّنة بالبرتقالي وعليها * مطلوبة.",
                "• الحالة التي فيها أكثر من منتج: اكتب كل منتج في سطر، وكرر نفس «رقم الحالة» ونفس الاسم والطبيب والعيادة والتاريخ.",
                "• «رقم الحالة» فارغ = حالة مستقلة لكل سطر، ويُعطى رقمها تلقائياً.",
                "• رقم حالة موجود مسبقاً في البرنامج لنفس السنة لا يُستورد مرة ثانية (يظهر كخطأ في المعاينة).",
                "• الطبيب أو العيادة أو المنتج غير المسجل: يظهر في المعاينة، وتختار هناك إنشاءه جديداً أو مطابقته مع اسم موجود.",
                "• الأسنان بالترقيم الدولي (FDI): الربع ثم رقم السن — 11–18 علوي يمين، 21–28 علوي يسار، 31–38 سفلي يسار، 41–48 سفلي يمين. اللبنية 51–85.",
                "• الجسر: أرقام الأسنان المتجاورة بينها «-» والدمية يُضاف لها p، مثل 34-35p-36. عدة أسنان مفردة: 11 21.",
                "• المنتج المسعّر بالسن: إذا تُرك «العدد» فارغاً يصبح العدد = عدد الأسنان (الجسر من 3 أسنان = 3).",
                "• السعر فارغ = يُؤخذ من قائمة الأسعار. الحالة «مجاني» = صفر.",
                "• الإعادة: سعرها كما في عمود السعر؛ اتركه فارغاً أو 0 لتكون مجانية للطبيب.",
                "• لا يُحفظ شيء قبل الضغط على «تأكيد الاستيراد» في صفحة المعاينة، ويمكن التراجع عن الاستيراد لاحقاً.",
                "",
                "مثال (لا تنسخه إلى ورقة الحالات كما هو):"
            };
            for (var i = 0; i < lines.Length; i++)
                help.Cell(i + 1, 1).Value = lines[i];
            help.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
            help.Column(1).Width = 110;

            var exampleTop = lines.Length + 2;
            var example = new object?[][]
            {
                [101, "مريض مثال", "د. اسم الطبيب", "اسم العيادة", "2026/11/01", "2026/11/08", "ZIRCONIA FULL ANATOMY", 2, 130m, "11 21", "", "", "عادي", "تاجان"],
                [101, "مريض مثال", "د. اسم الطبيب", "اسم العيادة", "2026/11/01", "2026/11/08", "ZIRCONIA FULL ANATOMY", null, null, "34-35p-36", "", "", "عادي", "جسر 3 وحدات — 35 دمية"],
                [null, "طفل مثال", "د. اسم الطبيب", "اسم العيادة", "2026/11/03", "", "STAINLESS STEEL CROWN", 1, null, "75", "", "", "عادي", "سن لبني — رقم الحالة تلقائي"],
                [null, "مريض ثالث", "د. اسم الطبيب", "اسم العيادة", "2026/11/03", "", "ZIRCONIA FULL ANATOMY", 1, null, "46", "نعم", "كسر", "إعادة", ""]
            };
            for (var i = 0; i < Columns.Length; i++)
            {
                var c = help.Cell(exampleTop, i + 2);
                c.Value = Columns[i].Title;
                c.Style.Font.SetBold();
                c.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E9ECEF"));
                help.Column(i + 2).Width = Math.Max(10, Columns[i].Width - 4);
            }
            for (var r = 0; r < example.Length; r++)
                for (var i = 0; i < example[r].Length; i++)
                {
                    var v = example[r][i];
                    var cell = help.Cell(exampleTop + 1 + r, i + 2);
                    if (v is int n) cell.Value = n;
                    else if (v is decimal d) cell.Value = d;
                    else if (v != null) cell.Value = v.ToString();
                }

            ws.SetTabActive();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ───────────────────────── القراءة والمعاينة ─────────────────────────

        public async Task<CaseImportPreview> PreviewAsync(Stream excel, string fileName)
        {
            var preview = new CaseImportPreview { FileName = fileName };

            XLWorkbook wb;
            try
            {
                wb = new XLWorkbook(excel);
            }
            catch (Exception ex) when (ex is ClosedXML.Excel.Exceptions.ClosedXMLException or InvalidDataException or IOException or FormatException)
            {
                preview.FileErrors.Add("تعذّرت قراءة الملف — تأكد أنه ملف Excel (.xlsx) صحيح وغير محمي بكلمة مرور.");
                return preview;
            }

            using (wb)
            {
                ReadRows(wb, preview);
            }

            if (preview.FileErrors.Count > 0)
                return preview;

            await LoadLookupsAsync(preview);
            GroupCases(preview);
            await CheckExistingCaseNumbersAsync(preview);
            ResolveNames(preview);
            return preview;
        }

        private static void ReadRows(XLWorkbook wb, CaseImportPreview preview)
        {
            var sheet = wb.Worksheets.FirstOrDefault(s => ArabicText.Normalize(s.Name) == ArabicText.Normalize(CasesSheetName))
                        ?? wb.Worksheets.FirstOrDefault();
            if (sheet == null)
            {
                preview.FileErrors.Add("الملف لا يحتوي أي ورقة.");
                return;
            }

            // ربط الأعمدة بعناوينها في الصف الأول (الترتيب غير مهم).
            var map = new Dictionary<string, int>();
            var header = sheet.Row(1);
            var lastCol = header.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var c = 1; c <= lastCol; c++)
            {
                var key = HeaderKey(header.Cell(c).GetString());
                foreach (var col in Columns)
                {
                    if (map.ContainsKey(col.Key)) continue;
                    if (col.AllTitles.Any(t => HeaderKey(t) == key))
                        map[col.Key] = c;
                }
            }

            var missing = Columns.Where(c => c.Required && !map.ContainsKey(c.Key)).Select(c => c.Title).ToList();
            if (missing.Count > 0)
            {
                preview.FileErrors.Add("هذا الملف ليس قالب استيراد الحالات، أو أن عناوين الأعمدة تغيّرت. الأعمدة الناقصة: "
                                       + string.Join("، ", missing) + ". نزّل القالب من صفحة الاستيراد واملأه.");
                return;
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            var today = DateTime.Today;

            for (var r = 2; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                string Text(string key) => map.TryGetValue(key, out var c) ? CellText(row.Cell(c)) : string.Empty;
                IXLCell? Cell(string key) => map.TryGetValue(key, out var c) ? row.Cell(c) : null;

                // صف فارغ تماماً يُتجاهل
                if (Columns.All(col => string.IsNullOrWhiteSpace(Text(col.Key))))
                    continue;

                if (preview.Rows.Count >= MaxRows)
                {
                    preview.FileErrors.Add($"الملف يحتوي أكثر من {MaxRows} سطر — قسّمه إلى أكثر من ملف.");
                    return;
                }

                var item = new ImportRow { RowNumber = r };

                var caseNo = Text("CaseNumber");
                if (caseNo.Length > 0)
                {
                    if (int.TryParse(ToLatinDigits(caseNo), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                        item.CaseNumber = n;
                    else
                        item.Errors.Add("رقم الحالة يجب أن يكون رقماً صحيحاً.");
                }

                item.Patient = Text("Patient");
                item.Doctor = Text("Doctor");
                item.Clinic = Text("Clinic");
                item.Product = Text("Product");
                item.Notes = NullIfEmpty(Text("Notes"));
                if (item.Patient.Length == 0) item.Errors.Add("اسم الحالة مطلوب.");
                if (item.Patient.Length > 150) item.Errors.Add("اسم الحالة أطول من 150 حرفاً.");
                if (item.Doctor.Length == 0) item.Errors.Add("الطبيب مطلوب.");
                if (item.Clinic.Length == 0) item.Errors.Add("العيادة مطلوبة.");
                if (item.Product.Length == 0) item.Errors.Add("المنتج مطلوب.");
                if (item.Notes?.Length > 500) item.Errors.Add("الملاحظات أطول من 500 حرف.");

                item.Received = ReadDate(Cell("Received"), out var badReceived);
                if (item.Received == null)
                    item.Errors.Add(badReceived ? "تاريخ الاستلام غير مفهوم — اكتبه مثل 2026/11/17." : "تاريخ الاستلام مطلوب.");
                else if (item.Received.Value.Year < 2000 || item.Received.Value > today.AddDays(31))
                    item.Errors.Add("تاريخ الاستلام غير منطقي.");

                item.Due = ReadDate(Cell("Due"), out var badDue);
                if (badDue) item.Errors.Add("موعد التسليم غير مفهوم — اكتبه مثل 2026/11/20.");

                var qty = Text("Quantity");
                item.QuantityGiven = qty.Length > 0;
                if (qty.Length == 0) item.Quantity = 1;
                else if (decimal.TryParse(ToLatinDigits(qty), NumberStyles.Number, CultureInfo.InvariantCulture, out var q)
                         && q >= 1 && q <= 10000 && q == Math.Floor(q))
                    item.Quantity = (int)q;
                else
                    item.Errors.Add("العدد يجب أن يكون رقماً صحيحاً من 1 فأكثر.");

                var price = Text("Price");
                if (price.Length > 0)
                {
                    if (decimal.TryParse(ToLatinDigits(price).Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p >= 0)
                        item.Price = Math.Round(p, 2);
                    else
                        item.Errors.Add("السعر يجب أن يكون رقماً (0 أو أكثر).");
                }

                var teeth = Text("Teeth");
                if (teeth.Length > 0)
                {
                    var parsed = TeethCode.Parse(teeth);
                    if (!parsed.IsValid) item.Errors.AddRange(parsed.Errors.Select(e => "الأسنان: " + e));
                    else if (parsed.Count > 0)
                    {
                        item.Teeth = TeethCode.Serialize(parsed);
                        item.TeethCount = parsed.Count;
                    }
                }

                var reason = Text("RemakeReason");
                if (reason.Length > 0)
                {
                    item.RemakeReason = RemakeReason.All.FirstOrDefault(x => ArabicText.Normalize(x) == ArabicText.Normalize(reason));
                    if (item.RemakeReason == null) item.Errors.Add("سبب الإعادة يجب أن يكون من القائمة: " + string.Join("، ", RemakeReason.All) + ".");
                }
                item.IsRemake = IsYes(Text("Remake")) || item.RemakeReason != null;

                var status = Text("Status");
                if (status.Length == 0) item.Status = CaseStatus.Normal;
                else
                {
                    item.Status = CaseStatus.All.FirstOrDefault(x => ArabicText.Normalize(x) == ArabicText.Normalize(status)) ?? string.Empty;
                    if (item.Status.Length == 0) item.Errors.Add("الحالة يجب أن تكون: " + string.Join(" / ", CaseStatus.All) + ".");
                }
                if (item.Status == CaseStatus.Remake) item.IsRemake = true;

                preview.Rows.Add(item);
            }

            if (preview.Rows.Count == 0)
                preview.FileErrors.Add("ورقة «الحالات» فارغة — لا توجد أسطر للاستيراد.");
        }

        private async Task LoadLookupsAsync(CaseImportPreview preview)
        {
            preview.Doctors = await _context.Doctors.AsNoTracking().OrderBy(d => d.Name)
                .Select(d => new NamedRef(d.Id, d.Name)).ToListAsync();
            preview.Clinics = await _context.Clinics.AsNoTracking().OrderBy(c => c.Name)
                .Select(c => new NamedRef(c.Id, c.Name)).ToListAsync();
            preview.Products = await _context.ServiceTypes.AsNoTracking().OrderBy(s => s.Name)
                .Select(s => new NamedRef(s.Id, s.Name)).ToListAsync();
        }

        /// <summary>تجميع الأسطر في حالات: نفس رقم الحالة ونفس السنة = حالة واحدة؛ بدون رقم = حالة لكل سطر.</summary>
        private static void GroupCases(CaseImportPreview preview)
        {
            var groups = new Dictionary<string, ImportCase>();
            foreach (var row in preview.Rows)
            {
                var year = row.Received?.Year ?? 0;
                var key = row.CaseNumber.HasValue ? $"N:{row.CaseNumber}:{year}" : $"R:{row.RowNumber}";
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new ImportCase { CaseNumber = row.CaseNumber, Year = year };
                    groups[key] = group;
                    preview.Cases.Add(group);
                }
                group.Rows.Add(row);
            }

            // أسطر الحالة الواحدة يجب أن تتفق في بيانات الرأس.
            foreach (var group in preview.Cases.Where(g => g.Rows.Count > 1))
            {
                var first = group.Rows[0];
                bool Same(Func<ImportRow, string?> f) =>
                    group.Rows.All(r => ArabicText.Normalize(f(r)) == ArabicText.Normalize(f(first)));

                if (!Same(r => r.Patient) || !Same(r => r.Doctor) || !Same(r => r.Clinic)
                    || group.Rows.Any(r => r.Received?.Date != first.Received?.Date)
                    || !Same(r => r.Status))
                {
                    group.Errors.Add($"أسطر الحالة رقم {group.CaseNumber} مختلفة في الاسم أو الطبيب أو العيادة أو التاريخ أو الحالة — يجب أن تتطابق.");
                }
            }
        }

        private async Task CheckExistingCaseNumbersAsync(CaseImportPreview preview)
        {
            var numbered = preview.Cases.Where(c => c.CaseNumber.HasValue).ToList();
            if (numbered.Count == 0) return;

            var numbers = numbered.Select(c => c.CaseNumber!.Value).Distinct().ToList();
            var existing = await _context.LabCases.AsNoTracking()
                .Where(c => c.CaseNumber != null && numbers.Contains(c.CaseNumber.Value))
                .Select(c => new { c.CaseNumber, c.Year })
                .ToListAsync();
            var set = existing.Select(e => (e.CaseNumber!.Value, e.Year)).ToHashSet();

            foreach (var group in numbered)
                if (set.Contains((group.CaseNumber!.Value, group.Year)))
                    group.Errors.Add($"الحالة رقم {group.CaseNumber} لسنة {group.Year} موجودة مسبقاً في البرنامج — لن تُستورد مرة ثانية.");
        }

        /// <summary>مطابقة الأطباء/العيادات/المنتجات بالأسماء الموجودة، وجمع غير المسجل مع اقتراح أقرب اسم.</summary>
        private static void ResolveNames(CaseImportPreview preview)
        {
            var unknown = new Dictionary<(NameKind, string), UnknownName>();
            // الأسماء غير المسجلة تُجمع فقط من الحالات السليمة؛ الحالات التي فيها أخطاء تُتخطى أصلاً.
            var validRows = preview.Cases.Where(c => !c.HasErrors).SelectMany(c => c.Rows).Select(r => r.RowNumber).ToHashSet();

            void Resolve(NameKind kind, IEnumerable<ImportRow> rows, Func<ImportRow, string> get,
                Action<ImportRow, Guid> set, List<NamedRef> existing, Func<string?, string> norm)
            {
                var index = existing.GroupBy(e => norm(e.Name)).ToDictionary(g => g.Key, g => g.First());
                foreach (var row in rows)
                {
                    var name = get(row);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var key = norm(name);
                    if (index.TryGetValue(key, out var match))
                    {
                        set(row, match.Id);
                        continue;
                    }

                    if (!validRows.Contains(row.RowNumber))
                        continue;

                    if (!unknown.TryGetValue((kind, key), out var u))
                    {
                        var suggestion = existing
                            .Select(e => (e, n: norm(e.Name)))
                            .Where(x => ArabicText.IsClose(key, x.n))
                            .OrderBy(x => ArabicText.EditDistance(key, x.n))
                            .Select(x => x.e)
                            .FirstOrDefault();
                        u = new UnknownName { Kind = kind, Name = name.Trim(), Key = key, SuggestedId = suggestion?.Id, SuggestedName = suggestion?.Name };
                        unknown[(kind, key)] = u;
                    }
                    u.RowNumbers.Add(row.RowNumber);
                }
            }

            Resolve(NameKind.Doctor, preview.Rows, r => r.Doctor, (r, id) => r.DoctorId = id, preview.Doctors, ArabicText.NormalizePersonName);
            Resolve(NameKind.Clinic, preview.Rows, r => r.Clinic, (r, id) => r.ClinicId = id, preview.Clinics, ArabicText.Normalize);
            Resolve(NameKind.Product, preview.Rows, r => r.Product, (r, id) => r.ServiceTypeId = id, preview.Products, ArabicText.Normalize);

            preview.Unknown = unknown.Values.OrderBy(u => u.Kind).ThenBy(u => u.Name).ToList();
        }

        // ───────────────────────── الحفظ ─────────────────────────

        /// <summary>
        /// يعيد قراءة الملف والتحقق منه (لا يُعتمد على ما عُرض في المتصفح)، ثم يطبق اختيارات المستخدم للأسماء غير المسجلة،
        /// ويحفظ الحالات السليمة كلها في عملية واحدة. الحالات التي فيها أخطاء تُتخطى.
        /// </summary>
        public async Task<CaseImportResult> CommitAsync(Stream excel, string fileName, IReadOnlyList<NameChoice> choices, string? userName)
        {
            var preview = await PreviewAsync(excel, fileName);
            var result = new CaseImportResult { Preview = preview };
            if (preview.FileErrors.Count > 0)
            {
                result.Errors.AddRange(preview.FileErrors);
                return result;
            }

            var valid = preview.Cases.Where(c => !c.HasErrors).ToList();
            if (valid.Count == 0)
            {
                result.Errors.Add("لا توجد حالات سليمة للاستيراد — صحح الأخطاء في الملف وارفعه مرة أخرى.");
                return result;
            }

            var batch = new ImportBatch
            {
                Id = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                FileName = Path.GetFileName(fileName),
                UserName = userName
            };

            // الأسماء غير المسجلة: مطابقة مع موجود أو إنشاء جديد (يحمل رقم الاستيراد).
            var newDoctors = new Dictionary<string, Doctor>();
            var newClinics = new Dictionary<string, Clinic>();
            var newProducts = new Dictionary<string, ServiceType>();
            var chosen = new Dictionary<(NameKind, string), Guid>();

            foreach (var u in preview.Unknown)
            {
                var choice = choices.FirstOrDefault(c => c.Kind == u.Kind && c.Key == u.Key)?.Choice;
                var existingList = u.Kind switch
                {
                    NameKind.Doctor => preview.Doctors,
                    NameKind.Clinic => preview.Clinics,
                    _ => preview.Products
                };

                if (Guid.TryParse(choice, out var id))
                {
                    if (existingList.All(e => e.Id != id))
                    {
                        result.Errors.Add($"الاختيار لـ «{u.Name}» غير موجود — أعد رفع الملف.");
                        return result;
                    }
                    chosen[(u.Kind, u.Key)] = id;
                    continue;
                }

                // «new» أو بلا اختيار ← إنشاء جديد
                var now = DateTime.UtcNow;
                switch (u.Kind)
                {
                    case NameKind.Doctor:
                        var d = new Doctor { Id = Guid.NewGuid(), Name = Truncate(u.Name, 150), Created = now, ImportBatchId = batch.Id };
                        newDoctors[u.Key] = d;
                        chosen[(u.Kind, u.Key)] = d.Id;
                        break;
                    case NameKind.Clinic:
                        var c = new Clinic { Id = Guid.NewGuid(), Name = Truncate(u.Name, 150), Created = now, ImportBatchId = batch.Id };
                        newClinics[u.Key] = c;
                        chosen[(u.Kind, u.Key)] = c.Id;
                        break;
                    default:
                        var firstPrice = preview.Rows.FirstOrDefault(r => ArabicText.Normalize(r.Product) == u.Key && r.Price.HasValue)?.Price ?? 0m;
                        var s = new ServiceType { Id = Guid.NewGuid(), Name = Truncate(u.Name, 150), UnitPrice = firstPrice, Created = now, ImportBatchId = batch.Id };
                        newProducts[u.Key] = s;
                        chosen[(u.Kind, u.Key)] = s.Id;
                        break;
                }
            }

            foreach (var row in preview.Rows)
            {
                row.DoctorId ??= chosen.GetValueOrDefault((NameKind.Doctor, ArabicText.NormalizePersonName(row.Doctor)));
                row.ClinicId ??= chosen.GetValueOrDefault((NameKind.Clinic, ArabicText.Normalize(row.Clinic)));
                row.ServiceTypeId ??= chosen.GetValueOrDefault((NameKind.Product, ArabicText.Normalize(row.Product)));
            }

            // الأسعار المستخدمة: قائمة الأسعار العامة + أسعار الأطباء والعيادات الخاصة.
            var services = await _context.ServiceTypes.AsNoTracking().ToDictionaryAsync(s => s.Id);
            foreach (var p in newProducts.Values) services[p.Id] = p;
            var doctorPrices = await _context.DoctorPrices.AsNoTracking().ToDictionaryAsync(p => (p.DoctorId, p.ServiceTypeId), p => p.Price);
            var clinicPrices = await _context.ClinicPrices.AsNoTracking().ToDictionaryAsync(p => (p.ClinicId, p.ServiceTypeId), p => p.Price);

            var nextNumber = Math.Max(
                (await _context.LabCases.MaxAsync(c => (int?)c.CaseNumber) ?? 0),
                preview.Cases.Max(c => c.CaseNumber ?? 0)) + 1;

            var today = DateTime.Today;
            var cases = new List<LabCase>();

            foreach (var group in valid)
            {
                var head = group.Rows[0];
                var received = head.Received!.Value.Date;
                var due = group.Rows.Select(r => r.Due).FirstOrDefault(d => d.HasValue)?.Date;
                var labCase = new LabCase
                {
                    Id = Guid.NewGuid(),
                    CaseNumber = group.CaseNumber ?? nextNumber++,
                    PatientName = Truncate(head.Patient, 150),
                    DoctorId = head.DoctorId,
                    ClinicId = head.ClinicId,
                    ReceivedDate = received,
                    DeliveryDate = due,
                    DeliveredAt = due.HasValue && due.Value <= today ? due : null,
                    Year = received.Year,
                    Month = received.Month,
                    Status = head.Status,
                    Notes = group.Rows.Count == 1 ? head.Notes : null,
                    Created = DateTime.UtcNow,
                    ImportBatchId = batch.Id
                };

                var remakeSequence = 0;
                foreach (var row in group.Rows)
                {
                    services.TryGetValue(row.ServiceTypeId ?? Guid.Empty, out var service);
                    decimal Catalog()
                    {
                        if (service == null) return 0m;
                        if (labCase.DoctorId.HasValue && doctorPrices.TryGetValue((labCase.DoctorId.Value, service.Id), out var dp)) return dp;
                        if (labCase.ClinicId.HasValue && clinicPrices.TryGetValue((labCase.ClinicId.Value, service.Id), out var cp)) return cp;
                        return service.UnitPrice;
                    }

                    var isRemake = row.IsRemake || labCase.Status == CaseStatus.Remake;
                    decimal price;
                    if (labCase.Status == CaseStatus.Free)
                        price = 0m;
                    else if (isRemake)
                    {
                        // مثل نموذج الحالة: المستخدم يحدد — السعر من الملف، وبدون سعر = مجانية.
                        remakeSequence++;
                        price = row.Price ?? 0m;
                    }
                    else
                        price = row.Price ?? Catalog();

                    // سعر من الملف يختلف عن قائمة الأسعار = سعر يدوي لا يغيّره تحديث الأسعار
                    var isManual = !isRemake && service != null && price != 0m && row.Price.HasValue && price != Catalog();

                    labCase.Items.Add(new LabCaseItem
                    {
                        Id = Guid.NewGuid(),
                        LabCaseId = labCase.Id,
                        ServiceTypeId = service?.Id,
                        ProductName = service?.Name ?? row.Product,
                        // المنتج المسعّر بالسن: العدد من الأسنان إذا لم يُكتب العدد في الملف
                        Quantity = !row.QuantityGiven && row.TeethCount > 0 && service?.PricedPerTooth == true ? row.TeethCount : row.Quantity,
                        UnitPrice = price,
                        IsManualPrice = isManual,
                        UnitCost = service?.UnitCost ?? 0m,
                        IsRemake = isRemake,
                        RemakeReason = isRemake ? row.RemakeReason : null,
                        RemakeSequence = isRemake ? remakeSequence : 0,
                        Teeth = row.Teeth,
                        ToothPosition = row.Teeth == null ? null : Truncate(TeethCode.ToDisplay(row.Teeth) ?? "", 100),
                        Notes = group.Rows.Count > 1 ? row.Notes : null,
                        Created = DateTime.UtcNow
                    });
                }

                var teeth = labCase.Items.Select(i => i.ToothPosition).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
                labCase.ToothPosition = teeth.Count == 0 ? null : Truncate(string.Join(" | ", teeth), 100);
                cases.Add(labCase);
            }

            batch.CaseCount = cases.Count;
            batch.ItemCount = cases.Sum(c => c.Items.Count);
            batch.DoctorsCreated = newDoctors.Count;
            batch.ClinicsCreated = newClinics.Count;
            batch.ProductsCreated = newProducts.Count;

            // الحفظ كله في عملية واحدة؛ سجل التغييرات يحصل على سطر ملخص واحد بدل آلاف الأسطر.
            _context.AuditEnabled = false;
            _context.ImportBatches.Add(batch);
            _context.Doctors.AddRange(newDoctors.Values);
            _context.Clinics.AddRange(newClinics.Values);
            _context.ServiceTypes.AddRange(newProducts.Values);
            _context.LabCases.AddRange(cases);
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserName = userName,
                Action = AuditAction.Added,
                EntityType = nameof(ImportBatch),
                EntityLabel = "استيراد حالات",
                EntityId = batch.Id.ToString(),
                Summary = $"استيراد من «{batch.FileName}»",
                Changes = $"الحالات: {batch.CaseCount}\nالأسطر: {batch.ItemCount}\n" +
                          $"أطباء جدد: {batch.DoctorsCreated}\nعيادات جديدة: {batch.ClinicsCreated}\nمنتجات جديدة: {batch.ProductsCreated}\n" +
                          $"حالات متخطاة بسبب أخطاء: {preview.Cases.Count - valid.Count}"
            });
            await _context.SaveChangesAsync();
            _context.AuditEnabled = true;

            result.Batch = batch;
            result.SkippedCases = preview.Cases.Count - valid.Count;
            return result;
        }

        // ───────────────────────── التراجع ─────────────────────────

        /// <summary>
        /// يحذف حالات عملية الاستيراد وأسطرها، والأطباء/العيادات/المنتجات التي أنشأها ولم يُستخدم أي منها بعد ذلك.
        /// يُرفض إذا كانت أي حالة منها قد فُوترت.
        /// </summary>
        public async Task<UndoResult> UndoAsync(Guid batchId, string? userName)
        {
            var result = new UndoResult();
            var batch = await _context.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch == null) { result.Error = "عملية الاستيراد غير موجودة."; return result; }
            if (batch.IsUndone) { result.Error = "تم التراجع عن هذا الاستيراد مسبقاً."; return result; }

            var invoiced = await _context.LabCases.AsNoTracking()
                .Where(c => c.ImportBatchId == batchId && c.InvoiceId != null)
                .Select(c => c.CaseNumber).ToListAsync();
            if (invoiced.Count > 0)
            {
                result.Error = "لا يمكن التراجع: بعض حالات هذا الاستيراد مُفوترة (" + string.Join("، ", invoiced.Take(10)) +
                               (invoiced.Count > 10 ? "…" : "") + "). ألغِ الفواتير أولاً.";
                return result;
            }

            await using var tx = await _context.Database.BeginTransactionAsync();

            result.ItemsDeleted = await _context.LabCaseItems.Where(i => i.LabCase!.ImportBatchId == batchId).ExecuteDeleteAsync();
            result.CasesDeleted = await _context.LabCases.Where(c => c.ImportBatchId == batchId).ExecuteDeleteAsync();

            // الأطباء/العيادات/المنتجات الجديدة تُحذف فقط إذا لم يعد أي شيء يستخدمها.
            result.DoctorsDeleted = await _context.Doctors
                .Where(d => d.ImportBatchId == batchId
                            && !_context.LabCases.Any(c => c.DoctorId == d.Id)
                            && !_context.DoctorPrices.Any(p => p.DoctorId == d.Id)
                            && !_context.Invoices.Any(i => i.DoctorId == d.Id))
                .ExecuteDeleteAsync();
            result.ClinicsDeleted = await _context.Clinics
                .Where(c => c.ImportBatchId == batchId
                            && !_context.LabCases.Any(x => x.ClinicId == c.Id)
                            && !_context.ClinicPrices.Any(p => p.ClinicId == c.Id)
                            && !_context.Invoices.Any(i => i.ClinicId == c.Id)
                            && !_context.IncomeEntries.Any(i => i.ClinicId == c.Id)
                            && !_context.ClinicCreditEntries.Any(e => e.ClinicId == c.Id))
                .ExecuteDeleteAsync();
            result.ProductsDeleted = await _context.ServiceTypes
                .Where(s => s.ImportBatchId == batchId
                            && !_context.LabCaseItems.Any(i => i.ServiceTypeId == s.Id)
                            && !_context.DoctorPrices.Any(p => p.ServiceTypeId == s.Id)
                            && !_context.ClinicPrices.Any(p => p.ServiceTypeId == s.Id))
                .ExecuteDeleteAsync();

            result.Kept = (batch.DoctorsCreated - result.DoctorsDeleted)
                          + (batch.ClinicsCreated - result.ClinicsDeleted)
                          + (batch.ProductsCreated - result.ProductsDeleted);

            batch.UndoneAt = DateTime.UtcNow;
            batch.UndoneBy = userName;
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserName = userName,
                Action = AuditAction.Deleted,
                EntityType = nameof(ImportBatch),
                EntityLabel = "استيراد حالات",
                EntityId = batch.Id.ToString(),
                Summary = $"تراجع عن استيراد «{batch.FileName}»",
                Changes = $"حالات محذوفة: {result.CasesDeleted}\nأسطر محذوفة: {result.ItemsDeleted}\n" +
                          $"أطباء محذوفون: {result.DoctorsDeleted}\nعيادات محذوفة: {result.ClinicsDeleted}\nمنتجات محذوفة: {result.ProductsDeleted}\n" +
                          $"أُبقي عليها لأنها استُخدمت بعد الاستيراد: {result.Kept}"
            });
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return result;
        }

        // ───────────────────────── أدوات ─────────────────────────

        private static string CellText(IXLCell cell)
        {
            if (cell.IsEmpty()) return string.Empty;
            if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime().ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            if (cell.DataType == XLDataType.Number) return cell.GetDouble().ToString(CultureInfo.InvariantCulture);
            return cell.GetFormattedString().Trim();
        }

        private static readonly string[] DateFormats =
        [
            "yyyy/M/d", "yyyy-M-d", "yyyy.M.d", "d/M/yyyy", "d-M-yyyy", "d.M.yyyy",
            "yyyy/M/d H:mm", "d/M/yyyy H:mm", "yyyy/M/d h:mm:ss tt", "M/d/yyyy h:mm:ss tt"
        ];

        private static DateTime? ReadDate(IXLCell? cell, out bool invalid)
        {
            invalid = false;
            if (cell == null || cell.IsEmpty()) return null;

            if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime().Date;
            if (cell.DataType == XLDataType.Number)
            {
                var d = cell.GetDouble();
                if (d > 20000 && d < 80000) return DateTime.FromOADate(d).Date; // رقم تاريخ Excel
                invalid = true;
                return null;
            }

            var text = ToLatinDigits(cell.GetFormattedString().Trim());
            if (text.Length == 0) return null;
            if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed.Date;

            invalid = true;
            return null;
        }

        private static string ToLatinDigits(string s)
        {
            var chars = s.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] is >= '٠' and <= '٩') chars[i] = (char)('0' + (chars[i] - '٠'));
                else if (chars[i] is >= '۰' and <= '۹') chars[i] = (char)('0' + (chars[i] - '۰'));
                else if (chars[i] == '٫') chars[i] = '.';
                else if (chars[i] == '٬') chars[i] = ',';
            }
            return new string(chars).Trim();
        }

        private static bool IsYes(string s) =>
            ArabicText.Normalize(s) is "نعم" or "yes" or "y" or "1" or "true" or "✓" or "x" or "اعاده";

        private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }

    // ───────────────────────── النماذج ─────────────────────────

    public record ImportColumn(string Key, string Title, bool Required, int Width, string Help, params string[] Aliases)
    {
        public IEnumerable<string> AllTitles => Aliases.Prepend(Title);
    }

    public record NamedRef(Guid Id, string Name);

    public enum NameKind { Doctor = 0, Clinic = 1, Product = 2 }

    public class ImportRow
    {
        public int RowNumber { get; set; }
        public int? CaseNumber { get; set; }
        public string Patient { get; set; } = string.Empty;
        public string Doctor { get; set; } = string.Empty;
        public string Clinic { get; set; } = string.Empty;
        public DateTime? Received { get; set; }
        public DateTime? Due { get; set; }
        public string Product { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal? Price { get; set; }
        public bool QuantityGiven { get; set; }
        public int TeethCount { get; set; }
        public string? Teeth { get; set; }
        public bool IsRemake { get; set; }
        public string? RemakeReason { get; set; }
        public string Status { get; set; } = CaseStatus.Normal;
        public string? Notes { get; set; }
        public List<string> Errors { get; } = [];

        public Guid? DoctorId { get; set; }
        public Guid? ClinicId { get; set; }
        public Guid? ServiceTypeId { get; set; }
    }

    public class ImportCase
    {
        public int? CaseNumber { get; set; }
        public int Year { get; set; }
        public List<ImportRow> Rows { get; } = [];
        public List<string> Errors { get; } = [];
        public bool HasErrors => Errors.Count > 0 || Rows.Any(r => r.Errors.Count > 0);
    }

    public class UnknownName
    {
        public NameKind Kind { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public Guid? SuggestedId { get; set; }
        public string? SuggestedName { get; set; }
        public List<int> RowNumbers { get; } = [];

        public string KindLabel => Kind switch
        {
            NameKind.Doctor => "طبيب",
            NameKind.Clinic => "عيادة",
            _ => "منتج"
        };
    }

    /// <summary>اختيار المستخدم لاسم غير مسجل: "new" أو معرّف سجل موجود.</summary>
    public class NameChoice
    {
        public NameKind Kind { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Choice { get; set; }
    }

    public class CaseImportPreview
    {
        public string FileName { get; set; } = string.Empty;
        public List<string> FileErrors { get; } = [];
        public List<ImportRow> Rows { get; } = [];
        public List<ImportCase> Cases { get; } = [];
        public List<UnknownName> Unknown { get; set; } = [];
        public List<NamedRef> Doctors { get; set; } = [];
        public List<NamedRef> Clinics { get; set; } = [];
        public List<NamedRef> Products { get; set; } = [];

        public int ValidCaseCount => Cases.Count(c => !c.HasErrors);
        public int InvalidCaseCount => Cases.Count(c => c.HasErrors);
        public int ValidRowCount => Cases.Where(c => !c.HasErrors).Sum(c => c.Rows.Count);
    }

    public class CaseImportResult
    {
        public CaseImportPreview Preview { get; set; } = new();
        public ImportBatch? Batch { get; set; }
        public int SkippedCases { get; set; }
        public List<string> Errors { get; } = [];
        public bool Succeeded => Batch != null;
    }

    public class UndoResult
    {
        public string? Error { get; set; }
        public int CasesDeleted { get; set; }
        public int ItemsDeleted { get; set; }
        public int DoctorsDeleted { get; set; }
        public int ClinicsDeleted { get; set; }
        public int ProductsDeleted { get; set; }
        public int Kept { get; set; }
    }
}
