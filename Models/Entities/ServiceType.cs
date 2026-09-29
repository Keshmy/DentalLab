using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    public class ServiceType : BaseEntity
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "نوع العمل")]
        public string Name { get; set; } = string.Empty;

        /// <summary>السعر العام — يُستخدم لكل طبيب ليس له سعر خاص.</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 1000000)]
        [Display(Name = "السعر العام")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// تكلفة الوحدة على المعمل (بلوك، تي-بيس ...). تُستخدم في:
        /// الربح الحقيقي للوحدة، وخسارة الإعادة المجانية.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 1000000)]
        [Display(Name = "تكلفة الوحدة")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        [Display(Name = "ربح الوحدة")]
        public decimal UnitMargin => UnitPrice - UnitCost;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public ICollection<LabCaseItem> CaseItems { get; set; } = new List<LabCaseItem>();

        public ICollection<DoctorPrice> DoctorPrices { get; set; } = new List<DoctorPrice>();
    }
}
