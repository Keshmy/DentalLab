using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>مصروف واحد (سطر من عمود "المصاريف" في ملف الإكسل).</summary>
    public class Expense : BaseEntity
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

        [Display(Name = "البند")]
        public Guid? ExpenseCategoryId { get; set; }

        [ValidateNever]
        public ExpenseCategory? ExpenseCategory { get; set; }

        [Display(Name = "الحساب")]
        public Guid? FinancialAccountId { get; set; }

        [ValidateNever]
        public FinancialAccount? FinancialAccount { get; set; }

        /// <summary>تم دفعه فعلياً (يؤثر على الكاش). غير المدفوع يبقى التزاماً.</summary>
        [Display(Name = "تم الدفع")]
        public bool IsPaid { get; set; } = true;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }
}
