using System.ComponentModel.DataAnnotations;

namespace DentalLab.Models.Entities
{
    /// <summary>بنود المصاريف (مواد، إيجار، رواتب، عمولات مصرفية، أجهزة ...).</summary>
    public class ExpenseCategory : BaseEntity
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "بند المصروف")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// بند رأسمالي (أجهزة ومعدات): يخرج من الكاش لكنه لا يُحمَّل كامل على ربح الشهر.
        /// </summary>
        [Display(Name = "بند رأسمالي (أجهزة)")]
        public bool IsCapital { get; set; }

        [StringLength(300)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }

    public static class ExpenseCategoryNames
    {
        public const string Materials = "مواد ومستلزمات";
        public const string Rent = "إيجار";
        public const string Salaries = "رواتب ومكافآت";
        public const string Maintenance = "صيانة";
        public const string BankFees = "عمولات مصرفية";
        public const string OutsourcedLab = "معامل خارجية";
        public const string Transport = "نقل وشحن";
        public const string Utilities = "نت وخدمات";
        public const string PartnerShare = "نسبة شريك";
        public const string Equipment = "أجهزة ومعدات";
        public const string Other = "أخرى";

        public static readonly string[] All =
        [
            Materials, Rent, Salaries, Maintenance, BankFees,
            OutsourcedLab, Transport, Utilities, PartnerShare, Equipment, Other
        ];
    }
}
