using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Identity
{
    public class RegisterVM
    {
        [DataType(DataType.EmailAddress)]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        [Display(Name = "البريد الإلكتروني")]
        public required string Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public required string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور والتأكيد غير متطابقين")]
        public required string ConfirmPassword { get; set; }

        [Range(18, 100)]
        [Display(Name = "العمر")]
        public int Age { get; set; }
    }
}
