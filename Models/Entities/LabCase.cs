using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// رأس الحالة. المبالغ كلها محسوبة من الأسطر (<see cref="Items"/>) وليست مخزّنة هنا،
    /// حتى يبقى إجمالي الحالة مطابقاً دائماً لمجموع أسطرها.
    /// </summary>
    public class LabCase : BaseEntity
    {
        [Display(Name = "رقم الحالة")]
        public int? CaseNumber { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "اسم الحالة")]
        public string PatientName { get; set; } = string.Empty;

        [Display(Name = "الطبيب")]
        public Guid? DoctorId { get; set; }

        [ValidateNever]
        public Doctor? Doctor { get; set; }

        [Display(Name = "المركز / العيادة")]
        public Guid? ClinicId { get; set; }

        [ValidateNever]
        public Clinic? Clinic { get; set; }

        [Display(Name = "تاريخ الاستلام")]
        [DataType(DataType.Date)]
        public DateTime? ReceivedDate { get; set; }

        [Display(Name = "تاريخ التسليم")]
        [DataType(DataType.Date)]
        public DateTime? DeliveryDate { get; set; }

        [StringLength(100)]
        [Display(Name = "مكان الأسنان")]
        public string? ToothPosition { get; set; }

        [StringLength(50)]
        [Display(Name = "رقم المستند")]
        public string? DocumentNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "إيصال قبض رقم")]
        public string? ReceiptNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Display(Name = "الشهر")]
        public int Month { get; set; }

        [StringLength(30)]
        [Display(Name = "الحالة")]
        public string Status { get; set; } = CaseStatus.Normal;

        /// <summary>الفاتورة التي فُوترت فيها هذه الحالة (فارغة = لم تُفوتر بعد).</summary>
        [Display(Name = "الفاتورة")]
        public Guid? InvoiceId { get; set; }

        [ValidateNever]
        public Invoice? Invoice { get; set; }

        [ValidateNever]
        public ICollection<LabCaseItem> Items { get; set; } = new List<LabCaseItem>();

        [NotMapped]
        [Display(Name = "الإجمالي")]
        public decimal Total => Items.Sum(i => i.LineTotal);

        [NotMapped]
        [Display(Name = "عدد الوحدات")]
        public int Units => Items.Sum(i => i.Quantity);

        [NotMapped]
        [Display(Name = "عدد الإعادات")]
        public int RemakeCount => Items.Count(i => i.IsRemake);

        [NotMapped]
        public bool IsInvoiced => InvoiceId.HasValue;

        /// <summary>هل تدخل هذه الحالة في الإيرادات؟ الملغي لا يُفوتر.</summary>
        [NotMapped]
        public bool IsBillable => Status != CaseStatus.Cancelled;
    }

    public static class CaseStatus
    {
        public const string Normal = "عادي";
        public const string Paid = "مدفوع";
        public const string Free = "مجاني";
        public const string Cancelled = "ملغي";
        public const string Remake = "إعادة";

        public static readonly string[] All =
        [
            Normal, Paid, Free, Cancelled, Remake
        ];
    }
}
