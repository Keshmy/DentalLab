using System.ComponentModel.DataAnnotations;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// عملية استيراد حالات من ملف Excel. كل حالة/طبيب/عيادة/منتج أنشأه الاستيراد يحمل رقمها،
    /// فيمكن التراجع عن الاستيراد كاملاً لاحقاً.
    /// </summary>
    public class ImportBatch
    {
        public Guid Id { get; set; }

        [Display(Name = "الوقت")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [StringLength(256)]
        [Display(Name = "الملف")]
        public string FileName { get; set; } = string.Empty;

        [StringLength(256)]
        [Display(Name = "المستخدم")]
        public string? UserName { get; set; }

        [Display(Name = "الحالات")]
        public int CaseCount { get; set; }

        [Display(Name = "الأسطر")]
        public int ItemCount { get; set; }

        public int DoctorsCreated { get; set; }
        public int ClinicsCreated { get; set; }
        public int ProductsCreated { get; set; }

        [Display(Name = "أُلغي في")]
        public DateTime? UndoneAt { get; set; }

        [StringLength(256)]
        public string? UndoneBy { get; set; }

        public bool IsUndone => UndoneAt.HasValue;
    }
}
