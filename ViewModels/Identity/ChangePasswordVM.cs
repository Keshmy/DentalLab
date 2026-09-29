using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Identity
{
    public class ChangePasswordVM
    {
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور الحالية")]
        public required string CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور الجديدة")]
        public required string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("NewPassword", ErrorMessage = "كلمة المرور الجديدة والتأكيد غير متطابقين")]
        public required string ConfirmPassword { get; set; }
    }
}
