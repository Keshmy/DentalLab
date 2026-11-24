using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// تحويل مبلغ من حساب إلى حساب آخر (مثلاً من الكاش إلى المصرف).
    /// ينقص رصيد المحوَّل منه ويزيد رصيد المحوَّل إليه بنفس المبلغ،
    /// فلا يغيّر مجموع الصندوق ولا الربح.
    /// </summary>
    public class AccountTransfer : BaseEntity
    {
        [Required(ErrorMessage = "التاريخ مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "التاريخ")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Display(Name = "الشهر")]
        public int Month { get; set; }

        [Required(ErrorMessage = "اختر الحساب المحوَّل منه")]
        [Display(Name = "من حساب")]
        public Guid FromAccountId { get; set; }

        [ValidateNever]
        public FinancialAccount? FromAccount { get; set; }

        [Required(ErrorMessage = "اختر الحساب المحوَّل إليه")]
        [Display(Name = "إلى حساب")]
        public Guid ToAccountId { get; set; }

        [ValidateNever]
        public FinancialAccount? ToAccount { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000000, ErrorMessage = "أدخل مبلغاً أكبر من صفر (أرقام فقط)")]
        [Display(Name = "المبلغ")]
        public decimal Amount { get; set; }

        [StringLength(300)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }
}
