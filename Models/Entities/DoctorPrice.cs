using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    /// <summary>
    /// سعر خاص لطبيب معيّن على نوع عمل معيّن — يتجاوز السعر العام.
    /// </summary>
    public class DoctorPrice : BaseEntity
    {
        [Display(Name = "الطبيب")]
        public Guid DoctorId { get; set; }

        [ValidateNever]
        public Doctor? Doctor { get; set; }

        [Display(Name = "نوع العمل")]
        public Guid ServiceTypeId { get; set; }

        [ValidateNever]
        public ServiceType? ServiceType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 1000000)]
        [Display(Name = "السعر الخاص")]
        public decimal Price { get; set; }

        [StringLength(300)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }
}
