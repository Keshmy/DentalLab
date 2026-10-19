using System.ComponentModel.DataAnnotations;

namespace DentalLab.Models.Entities
{
    /// <summary>إعدادات النسخ الاحتياطي (صف واحد في قاعدة البيانات).</summary>
    public class BackupSettings : BaseEntity
    {
        public const string DefaultFolder = @"C:\DentalLabBackups";

        [Required]
        [StringLength(400)]
        [Display(Name = "مجلد النسخ الاحتياطي")]
        public string FolderPath { get; set; } = DefaultFolder;

        [Display(Name = "نسخ احتياطي تلقائي يومي")]
        public bool AutoEnabled { get; set; } = true;

        /// <summary>ساعة التشغيل المحلية (0–23).</summary>
        [Range(0, 23)]
        [Display(Name = "ساعة النسخ التلقائي")]
        public int AutoHour { get; set; } = 2;

        [Range(0, 59)]
        [Display(Name = "دقيقة النسخ التلقائي")]
        public int AutoMinute { get; set; } = 0;

        /// <summary>آخر يوم محلي نُفِّذ فيه النسخ التلقائي بنجاح (yyyy-MM-dd).</summary>
        [StringLength(10)]
        public string? LastAutoBackupDate { get; set; }

        [Display(Name = "آخر نسخ يدوي/تلقائي")]
        public DateTime? LastBackupAt { get; set; }

        [StringLength(500)]
        public string? LastBackupMessage { get; set; }
    }
}
