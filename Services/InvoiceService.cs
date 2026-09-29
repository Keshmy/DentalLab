using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.ViewModels.Finance;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    /// <summary>
    /// بناء الفواتير الشهرية من أسطر الحالات.
    ///
    /// إجمالي السطر   = العدد × السعر
    /// إجمالي الفاتورة = Σ إجمالي الأسطر
    /// عدد الوحدات     = Σ العدد   (الإعادة تُحسب وحدة حتى لو كانت مجانية)
    ///
    /// المصدر واحد: فاتورة المركز وفواتير أطبائه تُحسب من نفس الأسطر،
    /// فلا يمكن أن تتناقض كما حدث في الملفات الورقية.
    /// </summary>
    public class InvoiceService
    {
        private readonly AppDbContext _context;

        public InvoiceService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>معاينة فاتورة شهر واحد لمركز (أو لطبيب داخل المركز).</summary>
        public async Task<InvoicePreviewVM> BuildPreviewAsync(Guid? clinicId, Guid? doctorId, int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var vm = new InvoicePreviewVM
            {
                ClinicId = clinicId,
                DoctorId = doctorId,
                Year = year,
                Month = month,
                FromDate = new DateTime(year, month, 1),
                ToDate = new DateTime(year, month, daysInMonth)
            };

            var site = await _context.SiteInfo.AsNoTracking().FirstOrDefaultAsync();
            if (site != null)
            {
                vm.LabName = site.Name;
                vm.LabPhone = site.Phone;
                vm.BankAccountName = site.BankAccountName;
                vm.BankName = site.BankName;
                vm.BankAccountNumber = site.BankAccountNumber;
            }

            if (clinicId.HasValue)
                vm.ClinicName = (await _context.Clinics.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == clinicId))?.Name ?? "بدون عيادة";

            if (doctorId.HasValue)
                vm.DoctorName = (await _context.Doctors.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == doctorId))?.Name;

            var query = _context.LabCaseItems
                .AsNoTracking()
                .Include(i => i.LabCase!).ThenInclude(c => c.Doctor)
                .Include(i => i.ServiceType)
                .Where(i => i.LabCase!.Year == year
                            && i.LabCase.Month == month
                            && i.LabCase.Status != CaseStatus.Cancelled);

            query = clinicId.HasValue
                ? query.Where(i => i.LabCase!.ClinicId == clinicId.Value)
                : query.Where(i => i.LabCase!.ClinicId == null);

            if (doctorId.HasValue)
                query = query.Where(i => i.LabCase!.DoctorId == doctorId.Value);

            var items = await query
                .OrderBy(i => i.LabCase!.Doctor!.Name)
                .ThenBy(i => i.LabCase!.CaseNumber)
                .ToListAsync();

            vm.ExistingInvoice = await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Year == year
                                          && i.Month == month
                                          && i.ClinicId == clinicId
                                          && i.DoctorId == doctorId
                                          && i.Status != InvoiceStatus.Cancelled);

            vm.Lines = items.Select(i => new InvoicePreviewLine
            {
                LabCaseId = i.LabCaseId,
                CaseNumber = i.LabCase?.CaseNumber,
                PatientName = i.LabCase?.PatientName,
                DoctorId = i.LabCase?.DoctorId,
                DoctorName = i.LabCase?.Doctor?.Name,
                ProductName = i.DisplayName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                UnitCost = i.UnitCost,
                IsRemake = i.IsRemake,
                AlreadyInvoiced = i.LabCase?.InvoiceId != null
                                  && i.LabCase.InvoiceId != vm.ExistingInvoice?.Id,
                ExistingInvoiceId = i.LabCase?.InvoiceId
            }).ToList();

            return vm;
        }

        /// <summary>
        /// إصدار الفاتورة: تُنسخ الأسطر نسخاً مثبَّتاً، ويُربط بها كل حالة لم تُفوتر بعد.
        /// </summary>
        public async Task<Invoice?> IssueAsync(Guid? clinicId, Guid? doctorId, int year, int month, string? notes)
        {
            var preview = await BuildPreviewAsync(clinicId, doctorId, year, month);
            if (preview.Lines.Count == 0)
                return null;

            if (preview.ExistingInvoice != null)
                return preview.ExistingInvoice;

            var nextNumber = (await _context.Invoices.MaxAsync(i => (int?)i.Number) ?? 0) + 1;

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                Number = nextNumber,
                ClinicId = clinicId,
                DoctorId = doctorId,
                Year = year,
                Month = month,
                FromDate = preview.FromDate,
                ToDate = preview.ToDate,
                IssueDate = DateTime.Now.Date,
                Status = InvoiceStatus.Issued,
                TotalAmount = preview.Total,
                TotalUnits = preview.Units,
                Notes = notes,
                Created = DateTime.UtcNow
            };

            foreach (var line in preview.Lines)
            {
                invoice.Lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    LabCaseId = line.LabCaseId,
                    CaseNumber = line.CaseNumber,
                    PatientName = line.PatientName,
                    DoctorName = line.DoctorName,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal,
                    IsRemake = line.IsRemake,
                    Created = DateTime.UtcNow
                });
            }

            _context.Invoices.Add(invoice);

            var caseIds = preview.Lines.Select(l => l.LabCaseId).Distinct().ToList();
            var cases = await _context.LabCases
                .Where(c => caseIds.Contains(c.Id) && c.InvoiceId == null)
                .ToListAsync();

            foreach (var labCase in cases)
            {
                labCase.InvoiceId = invoice.Id;
                labCase.Modified = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return invoice;
        }

        /// <summary>حذف فاتورة صادرة وفكّ ارتباط حالاتها.</summary>
        public async Task<bool> CancelAsync(Guid invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                return false;

            var cases = await _context.LabCases
                .Where(c => c.InvoiceId == invoiceId)
                .ToListAsync();

            foreach (var labCase in cases)
            {
                labCase.InvoiceId = null;
                labCase.Modified = DateTime.UtcNow;
            }

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>تحديث حالة الفاتورة بناءً على ما حُصّل منها.</summary>
        public async Task RefreshStatusAsync(Guid invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null || invoice.Status == InvoiceStatus.Cancelled)
                return;

            var paid = invoice.Payments.Where(p => p.IsReceived).Sum(p => p.Amount);

            invoice.Status = paid <= 0 ? InvoiceStatus.Issued
                : paid >= invoice.TotalAmount ? InvoiceStatus.Paid
                : InvoiceStatus.PartiallyPaid;

            await _context.SaveChangesAsync();
        }

        /// <summary>المراكز التي لها أعمال في شهر معيّن (لعرضها في قائمة الفواتير).</summary>
        public async Task<List<(Guid? ClinicId, string Name, decimal Total, int Units, bool Invoiced)>>
            GetMonthClinicsAsync(int year, int month)
        {
            var rows = await _context.LabCaseItems
                .AsNoTracking()
                .Where(i => i.LabCase!.Year == year
                            && i.LabCase.Month == month
                            && i.LabCase.Status != CaseStatus.Cancelled)
                .GroupBy(i => new { i.LabCase!.ClinicId, Name = i.LabCase.Clinic!.Name })
                .Select(g => new
                {
                    g.Key.ClinicId,
                    g.Key.Name,
                    Total = g.Sum(x => x.Quantity * x.UnitPrice),
                    Units = g.Sum(x => x.Quantity)
                })
                .ToListAsync();

            var issued = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Year == year && i.Month == month && i.DoctorId == null)
                .Select(i => i.ClinicId)
                .ToListAsync();

            return rows
                .Select(r => (r.ClinicId, r.Name ?? "بدون عيادة", r.Total, r.Units, issued.Contains(r.ClinicId)))
                .OrderByDescending(r => r.Total)
                .ToList();
        }
    }
}
