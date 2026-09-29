using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    public class UserProfile : BaseEntity
    {
        [Remote(action: "IsDisplayNameInUse", controller: "Account", AdditionalFields = nameof(Id))]
        [Display(Name = "الاسم الظاهر")]
        public string DisplayName { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }
        public bool Gender { get; set; }

        [ValidateNever]
        public ApplicationUser ApplicationUser { get; set; } = null!;

        [ForeignKey("ApplicationUser")]
        public string? UserId { get; set; }

        [NotMapped]
        public IFormFile? Image { get; set; }
    }
}
