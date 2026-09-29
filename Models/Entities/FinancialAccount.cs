using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// حساب أو صندوق تدخل منه وتخرج إليه الأموال (كاش، حساب عبدو، حساب شمال أفريقيا ...).
    /// </summary>
    public class FinancialAccount : BaseEntity
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "اسم الحساب")]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "النوع")]
        public string Kind { get; set; } = AccountKind.Cash;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "الرصيد الافتتاحي")]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [StringLength(300)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }

    public static class AccountKind
    {
        public const string Cash = "نقدي";
        public const string Bank = "مصرفي";

        public static readonly string[] All = [Cash, Bank];
    }

    public static class PaymentMethod
    {
        public const string Cash = "كاش";
        public const string Transfer = "حوالة";
        public const string Cheque = "شيك";

        public static readonly string[] All = [Cash, Transfer, Cheque];
    }
}
