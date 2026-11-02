using DentalLab.Models;
using DentalLab.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// كشف حساب مركز: نفس معادلة الذمم (رصيد افتتاحي + الأعمال − المحصَّل − الرصيد الدائن)
    /// لكن كحركات مؤرخة برصيد متحرك، حتى يطابق رصيدُ آخر الكشف رقمَ الذمم.
    /// </summary>
    public class StatementService
    {
        private readonly AppDbContext _context;

        public StatementService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ClinicStatementVM?> BuildAsync(Guid clinicId, DateTime? from, DateTime? to)
        {
            var clinic = await _context.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId);
            if (clinic == null)
                return null;

            var toDate = (to ?? DateTime.Now).Date;
            var fromDate = (from ?? new DateTime(toDate.Year, 1, 1)).Date;
            if (fromDate > toDate)
                (fromDate, toDate) = (toDate, fromDate);

            var all = new List<StatementLine>();

            // الأعمال: سطر لكل شهر (بتاريخ آخر يوم في الشهر) مع أرقام فواتيره.
            var work = await _context.LabCases
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && c.Status != CaseStatus.Cancelled)
                .GroupBy(c => new { c.Year, c.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Cases = g.Count(),
                    Units = g.SelectMany(c => c.Items).Sum(i => i.Quantity),
                    Total = g.SelectMany(c => c.Items).Sum(i => i.Quantity * i.UnitPrice)
                })
                .ToListAsync();

            var invoices = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.ClinicId == clinicId)
                .Select(i => new { i.Year, i.Month, i.Number })
                .ToListAsync();

            foreach (var w in work.Where(w => w.Total != 0 || w.Cases > 0))
            {
                var numbers = invoices.Where(i => i.Year == w.Year && i.Month == w.Month)
                                      .Select(i => i.Number).OrderBy(n => n).ToList();
                // الشهر الجاري يُؤرَّخ باليوم حتى لا يسقط من كشف ينتهي اليوم.
                var monthEnd = new DateTime(w.Year, w.Month, DateTime.DaysInMonth(w.Year, w.Month));
                all.Add(new StatementLine
                {
                    Date = monthEnd > DateTime.Today ? DateTime.Today : monthEnd,
                    Kind = StatementKind.Work,
                    Description = $"أعمال شهر {w.Month}/{w.Year} — {w.Cases} حالة، {w.Units} وحدة",
                    Reference = numbers.Count == 0 ? "لم تُفوتر" : "فاتورة " + string.Join("، ", numbers),
                    Debit = w.Total
                });
            }

            var payments = await _context.IncomeEntries
                .AsNoTracking()
                .Include(e => e.Invoice)
                .Where(e => e.ClinicId == clinicId && e.IsReceived)
                .ToListAsync();

            all.AddRange(payments.Select(p => new StatementLine
            {
                Date = (p.Date ?? new DateTime(p.Year, p.Month, 1)).Date,
                Kind = StatementKind.Payment,
                Description = string.IsNullOrWhiteSpace(p.Method) ? p.Description : $"{p.Description} ({p.Method})",
                Reference = p.Invoice != null ? $"فاتورة {p.Invoice.Number}" : null,
                Credit = p.Amount
            }));

            var credits = await _context.ClinicCreditEntries
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId)
                .ToListAsync();

            all.AddRange(credits.Select(c => new StatementLine
            {
                Date = c.Date.Date,
                Kind = StatementKind.Credit,
                Description = c.Notes ?? "رصيد دائن",
                Credit = c.Amount
            }));

            var ordered = all
                .OrderBy(l => l.Date)
                .ThenBy(l => l.Kind == StatementKind.Work ? 0 : 1)
                .ToList();

            var opening = clinic.OpeningBalance
                          + ordered.Where(l => l.Date < fromDate).Sum(l => l.Debit - l.Credit);

            var lines = ordered.Where(l => l.Date >= fromDate && l.Date <= toDate).ToList();
            var running = opening;
            foreach (var line in lines)
            {
                running += line.Debit - line.Credit;
                line.Balance = running;
            }

            return new ClinicStatementVM
            {
                ClinicId = clinic.Id,
                ClinicName = clinic.Name,
                ClinicPhone = clinic.Phone,
                From = fromDate,
                To = toDate,
                OpeningBalance = opening,
                Lines = lines
            };
        }
    }

    public static class StatementKind
    {
        public const string Work = "أعمال";
        public const string Payment = "تحصيل";
        public const string Credit = "رصيد دائن";
    }

    public class StatementLine
    {
        public DateTime Date { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }

        /// <summary>مدين: ما على المركز (الأعمال).</summary>
        public decimal Debit { get; set; }

        /// <summary>دائن: ما دفعه المركز أو أُضيف لرصيده.</summary>
        public decimal Credit { get; set; }

        public decimal Balance { get; set; }
    }

    public class ClinicStatementVM
    {
        public Guid ClinicId { get; set; }
        public string ClinicName { get; set; } = string.Empty;
        public string? ClinicPhone { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal OpeningBalance { get; set; }
        public List<StatementLine> Lines { get; set; } = [];

        public decimal TotalDebit => Lines.Sum(l => l.Debit);
        public decimal TotalCredit => Lines.Sum(l => l.Credit);
        public decimal ClosingBalance => OpeningBalance + TotalDebit - TotalCredit;
    }
}
