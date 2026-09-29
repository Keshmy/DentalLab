using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// سطر مُثبَّت في فاتورة صادرة — نسخة من سطر الحالة وقت الإصدار،
    /// لا يتأثر بأي تعديل لاحق على الحالة أو على الأسعار.
    /// </summary>
    public class InvoiceLine : BaseEntity
    {
        public Guid InvoiceId { get; set; }

        [ValidateNever]
        public Invoice? Invoice { get; set; }

        public Guid? LabCaseId { get; set; }

        [Display(Name = "رقم الحالة")]
        public int? CaseNumber { get; set; }

        [StringLength(150)]
        [Display(Name = "اسم الحالة")]
        public string? PatientName { get; set; }

        [StringLength(150)]
        [Display(Name = "الطبيب")]
        public string? DoctorName { get; set; }

        [StringLength(200)]
        [Display(Name = "المنتج")]
        public string? ProductName { get; set; }

        [Display(Name = "العدد")]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "السعر")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "الإجمالي")]
        public decimal LineTotal { get; set; }

        [Display(Name = "إعادة")]
        public bool IsRemake { get; set; }
    }
}
