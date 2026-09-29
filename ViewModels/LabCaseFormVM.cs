using DentalLab.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels
{
    /// <summary>سطر واحد في نموذج إدخال الحالة.</summary>
    public class LabCaseItemFormVM
    {
        public Guid? Id { get; set; }

        [Display(Name = "نوع العمل")]
        public Guid? ServiceTypeId { get; set; }

        [StringLength(200)]
        [Display(Name = "المنتج")]
        public string? ProductName { get; set; }

        [Range(1, 1000, ErrorMessage = "العدد يجب أن يكون 1 أو أكثر")]
        [Display(Name = "العدد")]
        public int Quantity { get; set; } = 1;

        [Range(0, 1000000)]
        [Display(Name = "السعر")]
        public decimal UnitPrice { get; set; }

        [Range(0, 1000000)]
        [Display(Name = "تكلفة الوحدة")]
        public decimal UnitCost { get; set; }

        [Display(Name = "إعادة")]
        public bool IsRemake { get; set; }

        [StringLength(30)]
        [Display(Name = "سبب الإعادة")]
        public string? RemakeReason { get; set; }

        [StringLength(100)]
        [Display(Name = "مكان الأسنان")]
        public string? ToothPosition { get; set; }

        [StringLength(300)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الإجمالي")]
        public decimal LineTotal => Quantity * UnitPrice;

        /// <summary>سطر فارغ تماماً — يُتجاهل عند الحفظ.</summary>
        public bool IsEmpty => ServiceTypeId == null && string.IsNullOrWhiteSpace(ProductName);
    }

    /// <summary>
    /// نموذج الحالة مع أسطرها. إجمالي الحالة يُحسب من الأسطر ولا يُدخل يدوياً،
    /// حتى يبقى مطابقاً للفاتورة دائماً.
    /// </summary>
    public class LabCaseFormVM
    {
        public Guid Id { get; set; }

        [Display(Name = "رقم الحالة")]
        public int? CaseNumber { get; set; }

        [Required(ErrorMessage = "اسم الحالة مطلوب")]
        [StringLength(150)]
        [Display(Name = "اسم الحالة")]
        public string PatientName { get; set; } = string.Empty;

        [Display(Name = "الطبيب")]
        public Guid? DoctorId { get; set; }

        [Display(Name = "المركز / العيادة")]
        public Guid? ClinicId { get; set; }

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

        [Display(Name = "الحالة")]
        public string Status { get; set; } = CaseStatus.Normal;

        public List<LabCaseItemFormVM> Items { get; set; } = [];

        /// <summary>الحالة مُفوترة — تعديل أسطرها لن يغيّر الفاتورة الصادرة.</summary>
        public bool IsInvoiced { get; set; }

        public int? InvoiceNumber { get; set; }

        [Display(Name = "إجمالي الحالة")]
        public decimal Total => Items.Where(i => !i.IsEmpty).Sum(i => i.LineTotal);

        [Display(Name = "عدد الوحدات")]
        public int Units => Items.Where(i => !i.IsEmpty).Sum(i => i.Quantity);
    }
}
