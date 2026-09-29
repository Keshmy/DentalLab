using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Identity
{
    public class CreateRoleVM
    {
        [Required(ErrorMessage = "اسم الصلاحية مطلوب")]
        [StringLength(30, MinimumLength = 3)]
        [Display(Name = "اسم الصلاحية")]
        public required string Name { get; set; }
    }
}
