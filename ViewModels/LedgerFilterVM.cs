using DentalLab.Models.Entities;

namespace DentalLab.ViewModels
{
    public class ImportResultVM
    {
        public int PricesImported { get; set; }
        public int PricesSkipped { get; set; }
        public int DoctorsCreated { get; set; }
        public int ClinicsCreated { get; set; }
        public int CasesImported { get; set; }
        public int CasesSkipped { get; set; }
        public int ItemsImported { get; set; }
        public int RoundingAdjustments { get; set; }

        /// <summary>أسطر أُضيفت إلى حالة موجودة (نفس رقم الحالة مكرَّر في الأرشيف = عمل إضافي لنفس الحالة).</summary>
        public int ItemsMergedIntoExistingCase { get; set; }

        /// <summary>أسطر كان عمود "السعر في الفاتورة" فيها فارغاً فتم تسعيرها من قائمة الأسعار.</summary>
        public int ItemsPricedFromList { get; set; }

        /// <summary>أسطر بلا سعر في الأرشيف وبلا مطابقة في قائمة الأسعار — تحتاج تسعيراً يدوياً.</summary>
        public int ItemsWithoutPrice { get; set; }

        // استيراد ملف الإيرادات والمصاريف
        public int ExpensesImported { get; set; }
        public int IncomeImported { get; set; }
        public int LoansImported { get; set; }
        public decimal ExpensesTotal { get; set; }
        public decimal IncomeTotal { get; set; }
        public decimal LoansTotal { get; set; }

        public List<string> Warnings { get; set; } = [];
    }

    /// <summary>سطر واحد في سجل الصندوق (حركة نقدية).</summary>
    public class LedgerRow
    {
        public DateTime? Date { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Account { get; set; }
        public decimal In { get; set; }
        public decimal Out { get; set; }
        public decimal Balance { get; set; }
        public bool IsPending { get; set; }
    }

    /// <summary>
    /// سجل الصندوق الشهري: كل الحركات النقدية بالترتيب مع رصيد متحرك،
    /// مثل الدفتر الورقي لكن محسوباً.
    /// </summary>
    public class LedgerFilterVM
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<int> AvailableYears { get; set; } = [];
        public List<LedgerRow> Rows { get; set; } = [];

        public decimal OpeningBalance { get; set; }
        public decimal TotalIn => Rows.Sum(r => r.In);
        public decimal TotalOut => Rows.Sum(r => r.Out);
        public decimal ClosingBalance => OpeningBalance + TotalIn - TotalOut;

        /// <summary>مبالغ مفوترة لم تُستلم بعد في هذا الشهر (لا تدخل الصندوق).</summary>
        public decimal PendingIncome { get; set; }
    }

    public static class LedgerKind
    {
        public const string Income = "إيراد";
        public const string Expense = "مصروف";
        public const string LoanGiven = "سلفة";
        public const string LoanRepaid = "سداد سلفة";
        public const string LoanBorrowed = "دين علينا";
        public const string LoanSettled = "سداد دين علينا";

        /// <summary>اسم الحركة في السجل حسب اتجاه السلفة.</summary>
        public static string ForLoan(string? direction) => direction switch
        {
            LoanDirection.Repaid => LoanRepaid,
            LoanDirection.Borrowed => LoanBorrowed,
            LoanDirection.Settled => LoanSettled,
            _ => LoanGiven
        };
    }
}
