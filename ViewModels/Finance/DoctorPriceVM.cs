using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Finance
{
    public class DoctorPriceRowVM
    {
        public Guid ServiceTypeId { get; set; }

        [Display(Name = "المنتج")]
        public string ServiceName { get; set; } = string.Empty;

        [Display(Name = "السعر العام")]
        public decimal GeneralPrice { get; set; }

        [Display(Name = "تكلفة الوحدة")]
        public decimal UnitCost { get; set; }

        /// <summary>فارغ = استخدم السعر العام.</summary>
        [Display(Name = "السعر الخاص")]
        [Range(0, 1000000, ErrorMessage = "سعر غير صالح")]
        public decimal? SpecialPrice { get; set; }

        public decimal EffectivePrice => SpecialPrice ?? GeneralPrice;

        public decimal Difference => EffectivePrice - GeneralPrice;

        public decimal Margin => EffectivePrice - UnitCost;

        public bool HasOverride => SpecialPrice.HasValue;
    }

    /// <summary>قائمة أسعار طبيب واحد: كل المنتجات مع السعر العام وإمكانية تخصيص السعر.</summary>
    public class DoctorPriceSheetVM
    {
        public Guid DoctorId { get; set; }

        [Display(Name = "الطبيب")]
        public string DoctorName { get; set; } = string.Empty;

        public List<DoctorPriceRowVM> Rows { get; set; } = [];

        public int OverrideCount => Rows.Count(r => r.HasOverride);
    }

    /// <summary>سطر في قائمة الأطباء بشاشة الأسعار.</summary>
    public class DoctorPriceSummaryVM
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public int OverrideCount { get; set; }
        public int CaseCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal MaterialCost { get; set; }
        public int RemakeUnits { get; set; }
        public decimal FreeRemakeCost { get; set; }

        public decimal GrossMargin => Revenue - MaterialCost;

        public decimal MarginPercent => Revenue == 0 ? 0 : Math.Round(GrossMargin * 100 / Revenue, 1);
    }
}
