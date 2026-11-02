using System.ComponentModel.DataAnnotations;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// سجل التغييرات: من أضاف/عدّل/حذف ماذا ومتى (الأسعار، الحالات، التحصيلات، الفواتير...).
    /// يُكتب تلقائياً من AppDbContext عند الحفظ، ولا يُعدَّل من الواجهة.
    /// </summary>
    public class AuditLog
    {
        public Guid Id { get; set; }

        [Display(Name = "الوقت")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(256)]
        [Display(Name = "المستخدم")]
        public string? UserName { get; set; }

        [StringLength(20)]
        [Display(Name = "العملية")]
        public string Action { get; set; } = string.Empty;

        /// <summary>اسم الكيان البرمجي (للفلترة).</summary>
        [StringLength(60)]
        public string EntityType { get; set; } = string.Empty;

        [StringLength(60)]
        [Display(Name = "النوع")]
        public string EntityLabel { get; set; } = string.Empty;

        [StringLength(60)]
        public string? EntityId { get; set; }

        [StringLength(300)]
        [Display(Name = "السجل")]
        public string? Summary { get; set; }

        /// <summary>سطر لكل حقل: «الحقل: القديم ← الجديد».</summary>
        [Display(Name = "التغييرات")]
        public string? Changes { get; set; }
    }

    public static class AuditAction
    {
        public const string Added = "إضافة";
        public const string Modified = "تعديل";
        public const string Deleted = "حذف";

        public static readonly string[] All = [Added, Modified, Deleted];
    }
}
