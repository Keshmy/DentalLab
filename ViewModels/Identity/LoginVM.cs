using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Identity
{
    public class LoginVM
    {
        [EmailAddress]
        [Display(Name = "البريد الإلكتروني")]
        public required string Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public required string Password { get; set; }

        [Display(Name = "تذكرني")]
        public bool RememberMe { get; set; }
    }
}
