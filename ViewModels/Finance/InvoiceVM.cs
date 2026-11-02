using DentalLab.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Finance
{
    /// <summary>سطر في معاينة الفاتورة — يقابل سطراً واحداً في الـ PDF.</summary>
    public class InvoicePreviewLine
    {
        public Guid LabCaseId { get; set; }

        [Display(Name = "رقم الحالة")]
        public int? CaseNumber { get; set; }

        [Display(Name = "اسم الحالة")]
        public string? PatientName { get; set; }

        [Display(Name = "الطبيب")]
        public string? DoctorName { get; set; }

        public Guid? DoctorId { get; set; }

        [Display(Name = "المنتج")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "العدد")]
        public int Quantity { get; set; }

        [Display(Name = "السعر")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "الإجمالي")]
        public decimal LineTotal => Quantity * UnitPrice;

        public decimal UnitCost { get; set; }

        public decimal LineCost => Quantity * UnitCost;

        [Display(Name = "إعادة")]
        public bool IsRemake { get; set; }

        /// <summary>هذه الحالة مرتبطة بفاتورة صادرة أخرى — تحذير من التفويتر المزدوج.</summary>
        public bool AlreadyInvoiced { get; set; }

        public Guid? ExistingInvoiceId { get; set; }
    }

    /// <summary>
    /// معاينة فاتورة شهرية قبل إصدارها. الأرقام هنا محسوبة من أسطر الحالات مباشرة،
    /// فلا يمكن أن تختلف فاتورة المركز عن فواتير أطبائه.
    /// </summary>
    public class InvoicePreviewVM
    {
        public Guid? ClinicId { get; set; }

        [Display(Name = "المركز / العيادة")]
        public string ClinicName { get; set; } = "بدون عيادة";

        public Guid? DoctorId { get; set; }

        [Display(Name = "الطبيب")]
        public string? DoctorName { get; set; }

        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Display(Name = "الشهر")]
        public int Month { get; set; }

        [Display(Name = "من تاريخ")]
        public DateTime FromDate { get; set; }

        [Display(Name = "إلى تاريخ")]
        public DateTime ToDate { get; set; }

        public List<InvoicePreviewLine> Lines { get; set; } = [];

        /// <summary>الأسطر التي ستدخل الفاتورة: تُستبعد الحالات المفوترة في فاتورة أخرى لمنع التفويتر المزدوج.</summary>
        public IEnumerable<InvoicePreviewLine> BillableLines => Lines.Where(l => !l.AlreadyInvoiced);

        [Display(Name = "إجمالي الفاتورة")]
        public decimal Total => BillableLines.Sum(l => l.LineTotal);

        [Display(Name = "عدد الوحدات")]
        public int Units => BillableLines.Sum(l => l.Quantity);

        [Display(Name = "تكلفة المواد")]
        public decimal TotalCost => BillableLines.Sum(l => l.LineCost);

        [Display(Name = "الربح الإجمالي")]
        public decimal GrossProfit => Total - TotalCost;

        [Display(Name = "وحدات الإعادة")]
        public int RemakeUnits => BillableLines.Where(l => l.IsRemake).Sum(l => l.Quantity);

        [Display(Name = "إعادات مجانية")]
        public decimal FreeRemakeCost => BillableLines.Where(l => l.IsRemake && l.UnitPrice == 0).Sum(l => l.LineCost);

        public int CaseCount => BillableLines.Select(l => l.LabCaseId).Distinct().Count();

        public bool HasAlreadyInvoiced => Lines.Any(l => l.AlreadyInvoiced);

        public bool HasBillableLines => BillableLines.Any();

        /// <summary>مجموع الأسطر المستبعدة لأنها مفوترة سابقاً (للعرض فقط).</summary>
        public decimal AlreadyInvoicedTotal => Lines.Where(l => l.AlreadyInvoiced).Sum(l => l.LineTotal);

        /// <summary>الفاتورة الصادرة لنفس (المركز/الطبيب/الشهر) إن وُجدت.</summary>
        public Invoice? ExistingInvoice { get; set; }

        // ترويسة الفاتورة
        public string? LabName { get; set; }
        public string? LabPhone { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }

        /// <summary>الأسطر مجمّعة بالطبيب كما في فواتير المراكز.</summary>
        public IEnumerable<IGrouping<string, InvoicePreviewLine>> ByDoctor =>
            Lines.GroupBy(l => l.DoctorName ?? "بدون طبيب");
    }

    /// <summary>شاشة اختيار فاتورة (المركز + الشهر).</summary>
    public class InvoiceFilterVM
    {
        public Guid? ClinicId { get; set; }
        public Guid? DoctorId { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;
        public List<int> AvailableYears { get; set; } = [];
        public List<Invoice> Issued { get; set; } = [];
    }
}
