using System.ComponentModel.DataAnnotations;

namespace DentalLab.ViewModels.Identity
{
    public class EditRoleVM
    {
        public required string Id { get; set; }

        [Required(ErrorMessage = "اسم الصلاحية مطلوب")]
        [StringLength(30, MinimumLength = 3)]
        [Display(Name = "اسم الصلاحية")]
        public required string RoleName { get; set; }
    }
}
