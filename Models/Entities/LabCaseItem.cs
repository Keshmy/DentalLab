using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// سطر واحد من أسطر الحالة (المنتج، العدد، السعر) — يقابل سطراً في الفاتورة.
    /// </summary>
    public class LabCaseItem : BaseEntity
    {
        public Guid LabCaseId { get; set; }

        [ValidateNever]
        public LabCase? LabCase { get; set; }

        [Display(Name = "نوع العمل")]
        public Guid? ServiceTypeId { get; set; }

        [ValidateNever]
        public ServiceType? ServiceType { get; set; }

        /// <summary>اسم المنتج كما يظهر في الفاتورة (يُثبَّت وقت الإدخال).</summary>
        [StringLength(200)]
        [Display(Name = "المنتج")]
        public string? ProductName { get; set; }

        [Range(1, 1000)]
        [Display(Name = "العدد")]
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// سعر الوحدة مُثبَّت وقت الإدخال، حتى لا يتغير أي سطر قديم عند تعديل قائمة الأسعار.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "السعر")]
        public decimal UnitPrice { get; set; }

        /// <summary>تكلفة الوحدة مُثبَّتة وقت الإدخال (تُنسخ من نوع العمل).</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "تكلفة الوحدة")]
        public decimal UnitCost { get; set; }

        [Display(Name = "إعادة")]
        public bool IsRemake { get; set; }

        [StringLength(30)]
        [Display(Name = "سبب الإعادة")]
        public string? RemakeReason { get; set; }

        /// <summary>رقم الإعادة لهذه الحالة: 1 = على المعمل (مجاناً)، 2 وما بعدها = على الطبيب.</summary>
        [Display(Name = "رقم الإعادة")]
        public int RemakeSequence { get; set; }

        [StringLength(100)]
        [Display(Name = "مكان الأسنان")]
        public string? ToothPosition { get; set; }

        [StringLength(300)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [NotMapped]
        [Display(Name = "الإجمالي")]
        public decimal LineTotal => Quantity * UnitPrice;

        [NotMapped]
        public string DisplayName => IsRemake
            ? (string.IsNullOrWhiteSpace(ProductName) ? "إعادة" : $"إعادة - {ProductName}")
            : (ProductName ?? ServiceType?.Name ?? string.Empty);
    }

    public static class RemakeReason
    {
        public const string LabFault = "خطأ المعمل";
        public const string Broken = "كسر";
        public const string Lost = "فقدان";
        public const string DoctorChange = "تعديل من الطبيب";
        public const string Other = "أخرى";

        public static readonly string[] All =
        [
            LabFault, Broken, Lost, DoctorChange, Other
        ];
    }
}
