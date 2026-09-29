using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Finance
{
    /// <summary>
    /// تقرير الإعادات لكل طبيب.
    ///
    /// الإعادة المحاسَبة  = وحدات إعادة بسعر > 0  → إيراد إضافي
    /// الإعادة المجانية   = وحدات إعادة بسعر 0    → خسارة = تكلفة المواد + إيراد ضائع
    /// </summary>
    public class RemakeRowVM
    {
        public Guid? DoctorId { get; set; }

        [Display(Name = "الطبيب")]
        public string DoctorName { get; set; } = string.Empty;

        [Display(Name = "إجمالي الوحدات")]
        public int TotalUnits { get; set; }

        [Display(Name = "وحدات الإعادة")]
        public int RemakeUnits { get; set; }

        [Display(Name = "إعادات محاسَبة")]
        public int ChargedUnits { get; set; }

        [Display(Name = "إعادات مجانية")]
        public int FreeUnits { get; set; }

        [Display(Name = "إيراد الإعادات")]
        public decimal ChargedAmount { get; set; }

        [Display(Name = "تكلفة المواد المهدرة")]
        public decimal FreeCost { get; set; }

        [Display(Name = "إيراد ضائع")]
        public decimal LostRevenue { get; set; }

        [Display(Name = "صافي أثر الإعادات")]
        public decimal NetImpact => ChargedAmount - FreeCost;

        [Display(Name = "نسبة الإعادة")]
        public decimal RemakeRatio => TotalUnits == 0
            ? 0
            : Math.Round((decimal)RemakeUnits * 100 / TotalUnits, 1);
    }

    public class RemakeReportVM
    {
        public int Year { get; set; }
        public int? Month { get; set; }
        public List<int> AvailableYears { get; set; } = [];
        public List<RemakeRowVM> Rows { get; set; } = [];
        public Dictionary<string, int> ByReason { get; set; } = [];

        public int RemakeUnits => Rows.Sum(r => r.RemakeUnits);
        public int TotalUnits => Rows.Sum(r => r.TotalUnits);
        public decimal ChargedAmount => Rows.Sum(r => r.ChargedAmount);
        public decimal FreeCost => Rows.Sum(r => r.FreeCost);
        public decimal LostRevenue => Rows.Sum(r => r.LostRevenue);
        public decimal NetImpact => Rows.Sum(r => r.NetImpact);

        public decimal RemakeRatio => TotalUnits == 0
            ? 0
            : Math.Round((decimal)RemakeUnits * 100 / TotalUnits, 1);
    }
}
