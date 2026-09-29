using System.ComponentModel.DataAnnotations;

namespace DentalLab.Models.Entities
{
    public class Doctor : BaseEntity
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "اسم الطبيب")]
        public string Name { get; set; } = string.Empty;

        [StringLength(30)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [StringLength(200)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public ICollection<LabCase> LabCases { get; set; } = new List<LabCase>();

        /// <summary>الأسعار الخاصة بهذا الطبيب (تتجاوز السعر العام).</summary>
        public ICollection<DoctorPrice> DoctorPrices { get; set; } = new List<DoctorPrice>();
    }
}
