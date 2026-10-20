using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// رصيد دائن لعيادة (مثلاً بعد إلغاء خدمة دُفعت مسبقاً).
    /// يُخصم من ذمم العيادة تلقائياً عبر مجموع هذه السجلات.
    /// </summary>
    public class ClinicCreditEntry : BaseEntity
    {
        [Required(ErrorMessage = "العيادة مطلوبة")]
        [Display(Name = "المركز / العيادة")]
        public Guid ClinicId { get; set; }

        [ValidateNever]
        public Clinic? Clinic { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000000, ErrorMessage = "أدخل مبلغاً أكبر من صفر")]
        [Display(Name = "المبلغ")]
        public decimal Amount { get; set; }

        [Display(Name = "التاريخ")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Now.Date;

        /// <summary>الحالة الملغاة التي نشأ منها الرصيد (إن وُجدت).</summary>
        [Display(Name = "الحالة")]
        public Guid? LabCaseId { get; set; }

        [ValidateNever]
        public LabCase? LabCase { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }
}
