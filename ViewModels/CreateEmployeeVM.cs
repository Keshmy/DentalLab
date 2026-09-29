using DentalLab.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels
{
    public class CreateEmployeeVM
    {
        [DataType(DataType.EmailAddress)]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        [Display(Name = "البريد الإلكتروني")]
        public required string Email { get; set; }

        [Display(Name = "العمر")]
        public int Age { get; set; }

        public Employee Employee { get; set; } = new();
    }
}
