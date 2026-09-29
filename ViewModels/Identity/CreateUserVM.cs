using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Identity
{
    public class CreateUserVM
    {
        [DataType(DataType.EmailAddress)]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        [Display(Name = "البريد الإلكتروني")]
        public required string Email { get; set; }

        [Range(18, 100)]
        [Display(Name = "العمر")]
        public int Age { get; set; }

        [ValidateNever]
        public List<IdentityRole> Roles { get; set; } = [];

        [Required(ErrorMessage = "الصلاحية مطلوبة")]
        [Display(Name = "الصلاحية")]
        public string RoleId { get; set; } = string.Empty;
    }
}
