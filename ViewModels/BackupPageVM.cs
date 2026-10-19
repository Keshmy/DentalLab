using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels
{
    public class BackupPageVM
    {
        [Required(ErrorMessage = "مسار المجلد مطلوب")]
        [Display(Name = "مجلد النسخ الاحتياطي")]
        public string FolderPath { get; set; } = @"C:\DentalLabBackups";

        [Display(Name = "تفعيل النسخ التلقائي اليومي")]
        public bool AutoEnabled { get; set; } = true;

        [Range(0, 23)]
        [Display(Name = "الساعة")]
        public int AutoHour { get; set; } = 2;

        [Range(0, 59)]
        [Display(Name = "الدقيقة")]
        public int AutoMinute { get; set; }

        public DateTime? LastBackupAt { get; set; }
        public string? LastBackupMessage { get; set; }
        public string? LastAutoBackupDate { get; set; }

        public List<string> SuggestedFolders { get; set; } = [];
        public List<BackupFileInfoVM> ExistingBackups { get; set; } = [];
        public bool FolderReady { get; set; }
        public string? FolderError { get; set; }
    }

    public class BackupFileInfoVM
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public long SizeBytes { get; set; }
        public string SizeDisplay => SizeBytes < 1024 * 1024
            ? $"{SizeBytes / 1024.0:N1} KB"
            : $"{SizeBytes / (1024.0 * 1024.0):N1} MB";
    }
}
