using System.ComponentModel.DataAnnotations;

namespace DentalLab.Models.Entities
{
    public class BaseEntity
    {
        public Guid Id { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy hh:mm:ss tt}")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy hh:mm:ss tt}")]
        public DateTime? Modified { get; set; }

        public string CreatedDateLocalTime => Created.ToLocalTime().ToString("dd-MM-yyyy hh:mm:ss tt");

        public string? ModifiedDateLocalTime => Modified?.ToLocalTime().ToString("dd-MM-yyyy hh:mm:ss tt");
    }
}
