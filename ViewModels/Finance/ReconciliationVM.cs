namespace DentalLab.ViewModels.Finance
{
    /// <summary>صفحة مطابقة الحسابات: كل فحص يقارن رقمين يجب أن يتساويا، ويسرد البنود التي تفسّر الفرق.</summary>
    public class ReconciliationVM
    {
        public DateTime AsOf { get; set; }
        public List<ReconciliationCheck> Checks { get; set; } = [];

        /// <summary>بنود مفتوحة لا تُعد خطأً (إيرادات لم تُستلم، مصروفات لم تُدفع) مع أعمارها.</summary>
        public List<ReconcilingItem> OpenItems { get; set; } = [];

        public int MatchedCount => Checks.Count(c => c.IsMatched);
        public int DifferenceCount => Checks.Count(c => !c.IsMatched);
        public int ItemCount => Checks.Sum(c => c.Items.Count);
        public int CountOf(string category) => Checks.Sum(c => c.Items.Count(i => i.Category == category));

        /// <summary>لا إقفال ما دام هناك فرق غير مسوّى في أي فحص.</summary>
        public bool CanClose => Checks.All(c => c.IsMatched);
    }

    public class ReconciliationCheck
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string LeftLabel { get; set; } = string.Empty;
        public decimal LeftValue { get; set; }
        public string RightLabel { get; set; } = string.Empty;
        public decimal RightValue { get; set; }

        /// <summary>الفحص يعدّ سجلات (مثل حالة الفواتير) لا مبالغ.</summary>
        public bool IsCount { get; set; }

        /// <summary>الشاشة التي يُصحَّح منها الفرق (Controller / Action).</summary>
        public string? FixController { get; set; }
        public string? FixAction { get; set; }
        public string? FixLabel { get; set; }

        public decimal Difference => LeftValue - RightValue;
        public bool IsMatched => Math.Abs(Difference) < 0.005m;

        public List<ReconcilingItem> Items { get; set; } = [];
    }

    public static class ReconcilingCategory
    {
        /// <summary>فرق توقيت — يزول وحده في الدورة العادية.</summary>
        public const string Timing = "توقيت";
        /// <summary>يحتاج تصحيحاً (قيد أو تعديل سجل).</summary>
        public const string Adjustment = "يحتاج تصحيحاً";
        /// <summary>غير مفسَّر — يحتاج تحقيقاً.</summary>
        public const string Investigate = "يحتاج تحقيقاً";
    }

    public class ReconcilingItem
    {
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
        public string Category { get; set; } = ReconcilingCategory.Investigate;

        /// <summary>السجل الذي يُفتح لتصحيح البند.</summary>
        public string? LinkController { get; set; }
        public string? LinkAction { get; set; }
        public Guid? LinkId { get; set; }

        public int? AgeDays { get; set; }

        /// <summary>شرائح العمر: 0–30 جاري، 31–60 متأخر، 61–90 متأخر جداً، 90+ راكد.</summary>
        public string AgeBucket => AgeDays switch
        {
            null => "—",
            <= 30 => "0–30",
            <= 60 => "31–60",
            <= 90 => "61–90",
            _ => "+90"
        };

        public string AgeAction => AgeDays switch
        {
            null or <= 30 => "متابعة",
            <= 60 => "تحقق من سبب التأخير",
            <= 90 => "تصعيد للمشرف",
            _ => "تصعيد للإدارة"
        };

        public string AgeCss => AgeDays switch
        {
            null or <= 30 => "bg-secondary",
            <= 60 => "bg-info",
            <= 90 => "bg-warning text-dark",
            _ => "bg-danger"
        };
    }
}
