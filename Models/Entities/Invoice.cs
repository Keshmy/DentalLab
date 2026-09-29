using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// فاتورة شهرية لمركز (وقد تكون لطبيب واحد داخل المركز).
    /// عند الإصدار تُثبَّت أسطرها حتى لا تتغير الفاتورة القديمة بعد ذلك.
    /// </summary>
    public class Invoice : BaseEntity
    {
        [Display(Name = "رقم الفاتورة")]
        public int Number { get; set; }

        [Display(Name = "المركز / العيادة")]
        public Guid? ClinicId { get; set; }

        [ValidateNever]
        public Clinic? Clinic { get; set; }

        /// <summary>اختياري: فاتورة خاصة بطبيب واحد داخل المركز.</summary>
        [Display(Name = "الطبيب")]
        public Guid? DoctorId { get; set; }

        [ValidateNever]
        public Doctor? Doctor { get; set; }

        [Display(Name = "من تاريخ")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Display(Name = "إلى تاريخ")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        [Display(Name = "تاريخ الإصدار")]
        [DataType(DataType.Date)]
        public DateTime IssueDate { get; set; } = DateTime.Now.Date;

        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Display(Name = "الشهر")]
        public int Month { get; set; }

        [StringLength(20)]
        [Display(Name = "الحالة")]
        public string Status { get; set; } = InvoiceStatus.Issued;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "إجمالي الفاتورة")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "عدد الوحدات")]
        public int TotalUnits { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

        [ValidateNever]
        public ICollection<IncomeEntry> Payments { get; set; } = new List<IncomeEntry>();

        [NotMapped]
        [Display(Name = "المسدد")]
        public decimal PaidAmount => Payments.Where(p => p.IsReceived).Sum(p => p.Amount);

        [NotMapped]
        [Display(Name = "المتبقي")]
        public decimal Outstanding => TotalAmount - PaidAmount;
    }

    public static class InvoiceStatus
    {
        public const string Draft = "مسودة";
        public const string Issued = "صادرة";
        public const string PartiallyPaid = "مسددة جزئياً";
        public const string Paid = "مسددة";
        public const string Cancelled = "ملغاة";

        public static readonly string[] All =
        [
            Draft, Issued, PartiallyPaid, Paid, Cancelled
        ];
    }
}
