using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// إيراد / تحصيل (سطر من عمود "ايرادات" في ملف الإكسل).
    /// غير المستلم يبقى ديناً على العيادة ولا يُحسب في الكاش.
    /// </summary>
    public class IncomeEntry : BaseEntity
    {
        [Display(Name = "التاريخ")]
        [DataType(DataType.Date)]
        public DateTime? Date { get; set; }

        [Required(ErrorMessage = "السنة مطلوبة")]
        [Range(2000, 2100, ErrorMessage = "أدخل سنة صحيحة (أرقام فقط)")]
        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Required(ErrorMessage = "الشهر مطلوب")]
        [Range(1, 12, ErrorMessage = "الشهر من 1 إلى 12")]
        [Display(Name = "الشهر")]
        public int Month { get; set; }

        [Required(ErrorMessage = "البيان مطلوب")]
        [StringLength(300)]
        [Display(Name = "البيان")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000000, ErrorMessage = "أدخل مبلغاً أكبر من صفر (أرقام فقط)")]
        [Display(Name = "المبلغ")]
        public decimal Amount { get; set; }

        [Display(Name = "المركز / العيادة")]
        public Guid? ClinicId { get; set; }

        [ValidateNever]
        public Clinic? Clinic { get; set; }

        [Display(Name = "الفاتورة")]
        public Guid? InvoiceId { get; set; }

        [ValidateNever]
        public Invoice? Invoice { get; set; }

        [Display(Name = "الحساب")]
        public Guid? FinancialAccountId { get; set; }

        [ValidateNever]
        public FinancialAccount? FinancialAccount { get; set; }

        [StringLength(20)]
        [Display(Name = "طريقة الدفع")]
        public string? Method { get; set; }

        [Display(Name = "تم الاستلام")]
        public bool IsReceived { get; set; } = true;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }
}
