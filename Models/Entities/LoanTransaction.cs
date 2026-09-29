using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// سلفة أو سداد سلفة. السلفة ليست مصروفاً: تنقص الكاش وتُسجَّل ذمة على الشخص،
    /// ولا تؤثر على الربح إلا إذا شُطبت.
    /// </summary>
    public class LoanTransaction : BaseEntity
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

        /// <summary>مطلوب إن لم يُختر موظف — التحقّق النهائي في الكنترولر.</summary>
        [StringLength(150)]
        [Display(Name = "الشخص")]
        public string PersonName { get; set; } = string.Empty;

        [Display(Name = "الموظف")]
        public Guid? EmployeeId { get; set; }

        [ValidateNever]
        public Employee? Employee { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000000, ErrorMessage = "أدخل مبلغاً أكبر من صفر (أرقام فقط)")]
        [Display(Name = "المبلغ")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "اختر نوع الحركة")]
        [StringLength(20)]
        [Display(Name = "الحركة")]
        public string Direction { get; set; } = LoanDirection.Given;

        [Display(Name = "الحساب")]
        public Guid? FinancialAccountId { get; set; }

        [ValidateNever]
        public FinancialAccount? FinancialAccount { get; set; }

        [StringLength(500)]
        [Display(Name = "البيان")]
        public string? Description { get; set; }

        /// <summary>عند الشطب يتحول المبلغ إلى خسارة (دين معدوم) ويُحمَّل على الأرباح.</summary>
        [Display(Name = "مشطوب (دين معدوم)")]
        public bool IsWrittenOff { get; set; }
    }

    /// <summary>
    /// اتجاه الحركة. نوعان من الذمم:
    ///   • لنا على الغير: سلفة نمنحها (كاش خارج) ثم يسددها (كاش داخل).
    ///   • علينا للغير: دين نقبضه أو يُطلب منا (كاش داخل) ثم نسدده (كاش خارج).
    /// في الحالتين لا تمس الحركة الربح، لأنها تحريك ذمة لا مصروف ولا إيراد.
    /// </summary>
    public static class LoanDirection
    {
        /// <summary>سلفة منحناها لشخص — كاش خارج، وتصبح ذمة لنا عليه.</summary>
        public const string Given = "سلفة ممنوحة";

        /// <summary>استرجعنا سلفة منحناها — كاش داخل.</summary>
        public const string Repaid = "سداد سلفة";

        /// <summary>دين علينا للغير (قبضنا مبلغاً أو التزمنا به) — كاش داخل.</summary>
        public const string Borrowed = "دين علينا";

        /// <summary>سددنا ديناً كان علينا — كاش خارج، وليس مصروفاً.</summary>
        public const string Settled = "سداد دين علينا";

        public static readonly string[] All = [Given, Repaid, Borrowed, Settled];

        /// <summary>الحركات التي تزيد الصندوق.</summary>
        public static readonly string[] CashInDirections = [Repaid, Borrowed];

        /// <summary>الحركات التي تنقص الصندوق.</summary>
        public static readonly string[] CashOutDirections = [Given, Settled];

        public static bool IsCashIn(string? direction) => direction == Repaid || direction == Borrowed;
    }
}
