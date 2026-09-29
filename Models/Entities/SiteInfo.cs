using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalLab.Models.Entities
{
    public class SiteInfo : BaseEntity
    {
        [StringLength(100, MinimumLength = 3)]
        [Display(Name = "اسم المختبر")]
        public required string Name { get; set; }

        [StringLength(200)]
        [Display(Name = "النشاط")]
        public required string Activity { get; set; }

        [StringLength(2000)]
        [Display(Name = "نبذة")]
        public required string About { get; set; }

        [Display(Name = "الشعار")]
        public string? LogoUrl { get; set; }

        // ===== بيانات ترويسة الفاتورة =====

        [StringLength(30)]
        [Display(Name = "هاتف المختبر")]
        public string? Phone { get; set; }

        [StringLength(150)]
        [Display(Name = "اسم صاحب الحساب")]
        public string? BankAccountName { get; set; }

        [StringLength(150)]
        [Display(Name = "المصرف")]
        public string? BankName { get; set; }

        [StringLength(50)]
        [Display(Name = "رقم الحساب")]
        public string? BankAccountNumber { get; set; }

        // ===== نسبة الشريك =====

        [StringLength(100)]
        [Display(Name = "اسم الشريك")]
        public string? PartnerName { get; set; }

        /// <summary>
        /// نسبة الشريك من صافي الربح (٪). الربح الصافي = الربح المحاسبي − نسبة الشريك.
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        [Display(Name = "نسبة الشريك من الربح ٪")]
        public decimal PartnerSharePercent { get; set; }

        [NotMapped]
        public IFormFile? Logo { get; set; }
    }
}
