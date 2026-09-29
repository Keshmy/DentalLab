using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Identity
{
    public class EditUserVM
    {
        public EditUserVM()
        {
            Roles = [];
        }

        public string Id { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "العمر")]
        public int Age { get; set; }

        [Display(Name = "الاسم الظاهر")]
        public string? DisplayName { get; set; }

        [Display(Name = "نهاية الإيقاف")]
        public DateTimeOffset? LockoutEnd { get; set; }

        public bool EmailConfirmed { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "تاريخ التعديل")]
        public DateTime? ModifiedDate { get; set; }

        [Display(Name = "آخر دخول")]
        public DateTime? LastAccessTime { get; set; }

        public List<string> Roles { get; set; }

        public DateTime CreatedDateLocalTime => CreatedDate.ToLocalTime();
        public DateTime? ModifiedDateLocalTime => ModifiedDate?.ToLocalTime();
        public DateTime? LastAccessTimeLocalTime => LastAccessTime?.ToLocalTime();
        public DateTimeOffset? LockoutEndTimeLocalTime => LockoutEnd?.ToLocalTime();
    }
}
