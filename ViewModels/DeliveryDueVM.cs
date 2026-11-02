using DentalLab.Models.Entities;

namespace DentalLab.ViewModels
{
    /// <summary>لوحة مواعيد التسليم.</summary>
    public class DeliveryDueVM
    {
        public DateTime Today { get; set; }
        public List<LabCase> Overdue { get; set; } = [];
        public List<LabCase> DueToday { get; set; } = [];
        public List<LabCase> Upcoming { get; set; } = [];
        public List<LabCase> DeliveredToday { get; set; } = [];

        /// <summary>حالات حديثة (آخر 30 يوماً) لم يُحدَّد لها موعد تسليم.</summary>
        public int WithoutDate { get; set; }
    }
}
