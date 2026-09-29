using DentalLab.Models.Entities;
using DentalLab.ViewModels.Finance;

namespace DentalLab.ViewModels
{
    public class DashboardVM
    {
        // ===== أعداد =====
        public int CasesThisMonth { get; set; }
        public int PendingDelivery { get; set; }
        public int TotalCases { get; set; }
        public int DoctorsCount { get; set; }
        public int ClinicsCount { get; set; }
        public int MonthUnits { get; set; }
        public int MonthRemakeUnits { get; set; }

        // ===== مال الشهر =====
        public decimal MonthRevenue { get; set; }
        public decimal MonthCollections { get; set; }
        public decimal MonthExpenses { get; set; }
        public decimal MonthNetProfit { get; set; }
        public decimal MonthFreeRemakeCost { get; set; }

        // ===== أرصدة مفتوحة =====
        public decimal TotalReceivables { get; set; }
        public decimal TotalLoanBalance { get; set; }
        public decimal CashBalance { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }

        public List<LabCase> RecentCases { get; set; } = [];
        public List<ClinicReceivableVM> TopReceivables { get; set; } = [];
        public List<AccountBalanceVM> Accounts { get; set; } = [];
    }
}
