using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    public class Clinic : BaseEntity
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "اسم العيادة")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [StringLength(30)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "رصيد افتتاحي (دين سابق)")]
        public decimal OpeningBalance { get; set; }

        public ICollection<LabCase> LabCases { get; set; } = new List<LabCase>();

        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

        public ICollection<IncomeEntry> Payments { get; set; } = new List<IncomeEntry>();
    }
}
