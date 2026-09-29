using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Finance
{
    /// <summary>سطر شهر واحد في تقرير الأرباح والخسائر.</summary>
    public class MonthlyFinanceRow
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public string MonthName => Month >= 1 && Month <= 12
            ? new[] { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" }[Month - 1]
            : Month.ToString();

        // ===== الجانب المحاسبي (الاستحقاق) =====

        /// <summary>الإيراد المستحق = مجموع (العدد × السعر) لكل أسطر حالات الشهر.</summary>
        [Display(Name = "الإيراد المفوتر")]
        public decimal Revenue { get; set; }

        [Display(Name = "عدد الوحدات")]
        public int Units { get; set; }

        /// <summary>تكلفة المواد للأعمال المنفَّذة (من تكلفة الوحدة) — لقياس ربح الوحدة فقط.</summary>
        [Display(Name = "تكلفة مواد الأعمال")]
        public decimal MaterialCost { get; set; }

        /// <summary>الربح الإجمالي على مستوى الوحدة = الإيراد − تكلفة المواد.</summary>
        [Display(Name = "الربح الإجمالي للوحدات")]
        public decimal GrossMargin => Revenue - MaterialCost;

        /// <summary>المصروفات التشغيلية (بدون الأجهزة وبدون نسبة الشريك، فهي تُحسب من الربح).</summary>
        [Display(Name = "المصروفات التشغيلية")]
        public decimal OperatingExpenses { get; set; }

        [Display(Name = "أجهزة ومعدات")]
        public decimal CapitalExpenses { get; set; }

        [Display(Name = "ديون معدومة")]
        public decimal WrittenOffLoans { get; set; }

        /// <summary>الربح قبل نسبة الشريك = الإيراد المفوتر − المصروفات التشغيلية − الديون المعدومة.</summary>
        [Display(Name = "الربح قبل النسبة")]
        public decimal AccrualProfit => Revenue - OperatingExpenses - WrittenOffLoans;

        /// <summary>نسبة الشريك ٪ (من إعدادات المختبر).</summary>
        public decimal PartnerSharePercent { get; set; }

        /// <summary>نسبة الشريك محسوبة من الربح — لا تُحسب على الخسارة.</summary>
        [Display(Name = "نسبة الشريك")]
        public decimal PartnerShare => AccrualProfit <= 0
            ? 0
            : Math.Round(AccrualProfit * PartnerSharePercent / 100m, 2);

        /// <summary>نسبة الشريك كما أُدخلت يدوياً كمصروف — للمقارنة مع المحسوبة.</summary>
        [Display(Name = "نسبة مدفوعة فعلاً")]
        public decimal PartnerShareRecorded { get; set; }

        /// <summary>صافي الربح النهائي = الربح قبل النسبة − نسبة الشريك.</summary>
        [Display(Name = "صافي الربح")]
        public decimal NetProfit => AccrualProfit - PartnerShare;

        // ===== الجانب النقدي =====

        [Display(Name = "التحصيلات")]
        public decimal Collections { get; set; }

        [Display(Name = "إيرادات غير مستلمة")]
        public decimal PendingIncome { get; set; }

        [Display(Name = "المصروفات المدفوعة")]
        public decimal PaidExpenses { get; set; }

        [Display(Name = "سلف ممنوحة")]
        public decimal LoansGiven { get; set; }

        [Display(Name = "سداد سلف")]
        public decimal LoansRepaid { get; set; }

        /// <summary>ديون علينا قبضناها — كاش داخل وليست إيراداً.</summary>
        [Display(Name = "ديون علينا (مقبوضة)")]
        public decimal LoansBorrowed { get; set; }

        /// <summary>سداد ديون كانت علينا — كاش خارج وليس مصروفاً.</summary>
        [Display(Name = "سداد ديون علينا")]
        public decimal LoansSettled { get; set; }

        [Display(Name = "الداخل نقداً")]
        public decimal CashIn => Collections + LoansRepaid + LoansBorrowed;

        [Display(Name = "الخارج نقداً")]
        public decimal CashOut => PaidExpenses + LoansGiven + LoansSettled;

        /// <summary>الربح النقدي = الداخل − الخارج (هذا ما يظهر في الصندوق فعلاً).</summary>
        [Display(Name = "الربح النقدي")]
        public decimal CashProfit => CashIn - CashOut;

        // ===== مؤشرات الجودة =====

        [Display(Name = "وحدات الإعادة")]
        public int RemakeUnits { get; set; }

        [Display(Name = "إعادات محاسَبة")]
        public decimal RemakeCharged { get; set; }

        /// <summary>قيمة الإعادات المجانية بالسعر العام — إيراد ضائع.</summary>
        [Display(Name = "إيراد ضائع بالإعادات")]
        public decimal FreeRemakeLoss { get; set; }

        /// <summary>تكلفة المواد المستهلكة في الإعادات المجانية — خسارة نقدية حقيقية.</summary>
        [Display(Name = "تكلفة الإعادات المجانية")]
        public decimal FreeRemakeCost { get; set; }

        public decimal RemakeRatio => Units == 0 ? 0 : Math.Round((decimal)RemakeUnits * 100 / Units, 1);

        public decimal AverageUnitPrice => Units == 0 ? 0 : Math.Round(Revenue / Units, 2);
    }

    public class FinanceSummaryVM
    {
        public int Year { get; set; }
        public int? Month { get; set; }

        public List<int> AvailableYears { get; set; } = new();
        public List<MonthlyFinanceRow> Months { get; set; } = new();

        // إجماليات الفترة
        public decimal Revenue => Months.Sum(m => m.Revenue);
        public int Units => Months.Sum(m => m.Units);
        public decimal MaterialCost => Months.Sum(m => m.MaterialCost);
        public decimal GrossMargin => Months.Sum(m => m.GrossMargin);
        public decimal OperatingExpenses => Months.Sum(m => m.OperatingExpenses);
        public decimal CapitalExpenses => Months.Sum(m => m.CapitalExpenses);
        public decimal WrittenOffLoans => Months.Sum(m => m.WrittenOffLoans);
        public decimal AccrualProfit => Months.Sum(m => m.AccrualProfit);
        public decimal PartnerShare => Months.Sum(m => m.PartnerShare);
        public decimal PartnerShareRecorded => Months.Sum(m => m.PartnerShareRecorded);
        public decimal NetProfit => Months.Sum(m => m.NetProfit);
        public decimal PartnerSharePercent { get; set; }
        public string? PartnerName { get; set; }

        public decimal Collections => Months.Sum(m => m.Collections);
        public decimal PendingIncome => Months.Sum(m => m.PendingIncome);
        public decimal PaidExpenses => Months.Sum(m => m.PaidExpenses);
        public decimal LoansGiven => Months.Sum(m => m.LoansGiven);
        public decimal LoansRepaid => Months.Sum(m => m.LoansRepaid);
        public decimal LoansBorrowed => Months.Sum(m => m.LoansBorrowed);
        public decimal LoansSettled => Months.Sum(m => m.LoansSettled);
        public decimal CashIn => Months.Sum(m => m.CashIn);
        public decimal CashOut => Months.Sum(m => m.CashOut);
        public decimal CashProfit => Months.Sum(m => m.CashProfit);

        public int RemakeUnits => Months.Sum(m => m.RemakeUnits);
        public decimal FreeRemakeLoss => Months.Sum(m => m.FreeRemakeLoss);
        public decimal FreeRemakeCost => Months.Sum(m => m.FreeRemakeCost);

        /// <summary>أرصدة مفتوحة لا تخص شهراً بعينه.</summary>
        public decimal TotalReceivables { get; set; }
        public decimal TotalLoanBalance { get; set; }

        /// <summary>
        /// جسر التسوية بين الربحين: الفرق يأتي من الذمم غير المحصلة،
        /// والسلف، وشراء الأجهزة، والمصروفات غير المدفوعة.
        /// </summary>
        public decimal Bridge => AccrualProfit - CashProfit;
    }

    public class ClinicReceivableVM
    {
        public Guid? ClinicId { get; set; }
        public string ClinicName { get; set; } = string.Empty;

        [Display(Name = "رصيد افتتاحي")]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "إجمالي الأعمال")]
        public decimal Invoiced { get; set; }

        [Display(Name = "المحصّل")]
        public decimal Collected { get; set; }

        [Display(Name = "المتبقي")]
        public decimal Balance => OpeningBalance + Invoiced - Collected;

        [Display(Name = "عدد الحالات")]
        public int CaseCount { get; set; }

        [Display(Name = "عدد الوحدات")]
        public int Units { get; set; }
    }

    public class LoanBalanceVM
    {
        public string PersonName { get; set; } = string.Empty;

        [Display(Name = "سلف ممنوحة")]
        public decimal Given { get; set; }

        [Display(Name = "المسدد")]
        public decimal Repaid { get; set; }

        [Display(Name = "مشطوب")]
        public decimal WrittenOff { get; set; }

        [Display(Name = "دين علينا")]
        public decimal Borrowed { get; set; }

        [Display(Name = "سددناه له")]
        public decimal Settled { get; set; }

        /// <summary>
        /// الرصيد الصافي مع هذا الشخص:
        /// موجب = له علينا لا شيء وهو مدين لنا، سالب = نحن مدينون له.
        /// </summary>
        [Display(Name = "المتبقي")]
        public decimal Balance => (Given - Repaid - WrittenOff) - (Borrowed - Settled);

        [Display(Name = "الجهة")]
        public string Side => Balance > 0 ? "لنا عليه" : Balance < 0 ? "علينا له" : "مسوّى";
    }

    public class AccountBalanceVM
    {
        public Guid AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;

        [Display(Name = "رصيد افتتاحي")]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "داخل")]
        public decimal In { get; set; }

        [Display(Name = "خارج")]
        public decimal Out { get; set; }

        [Display(Name = "الرصيد")]
        public decimal Balance => OpeningBalance + In - Out;
    }
}
