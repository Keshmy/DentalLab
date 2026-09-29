using DentalLab.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Models
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<SiteInfo> SiteInfo { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Clinic> Clinics { get; set; }
        public DbSet<ServiceType> ServiceTypes { get; set; }
        public DbSet<DoctorPrice> DoctorPrices { get; set; }
        public DbSet<LabCase> LabCases { get; set; }
        public DbSet<LabCaseItem> LabCaseItems { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLine> InvoiceLines { get; set; }
        public DbSet<FinancialAccount> FinancialAccounts { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<IncomeEntry> IncomeEntries { get; set; }
        public DbSet<LoanTransaction> LoanTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SiteInfo>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<UserProfile>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Employee>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Doctor>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Clinic>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<ServiceType>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<DoctorPrice>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<LabCase>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<LabCaseItem>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Invoice>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<InvoiceLine>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<FinancialAccount>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<ExpenseCategory>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Expense>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<IncomeEntry>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<LoanTransaction>().Property(x => x.Id).HasDefaultValueSql("NEWID()");

            modelBuilder.Entity<LabCase>()
                .HasIndex(c => new { c.CaseNumber, c.Year });

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.UserProfile)
                .WithOne(p => p.ApplicationUser)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Employee)
                .WithOne(e => e.ApplicationUser)
                .HasForeignKey<Employee>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LabCase>()
                .HasOne(c => c.Doctor)
                .WithMany(d => d.LabCases)
                .HasForeignKey(c => c.DoctorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LabCase>()
                .HasOne(c => c.Clinic)
                .WithMany(cl => cl.LabCases)
                .HasForeignKey(c => c.ClinicId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LabCase>()
                .HasOne(c => c.Invoice)
                .WithMany()
                .HasForeignKey(c => c.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull);

            // أسطر الحالة تُحذف مع الحالة
            modelBuilder.Entity<LabCaseItem>()
                .HasOne(i => i.LabCase)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.LabCaseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LabCaseItem>()
                .HasOne(i => i.ServiceType)
                .WithMany(s => s.CaseItems)
                .HasForeignKey(i => i.ServiceTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            // سعر خاص واحد لكل (طبيب، نوع عمل)
            modelBuilder.Entity<DoctorPrice>()
                .HasIndex(p => new { p.DoctorId, p.ServiceTypeId })
                .IsUnique();

            modelBuilder.Entity<DoctorPrice>()
                .HasOne(p => p.Doctor)
                .WithMany(d => d.DoctorPrices)
                .HasForeignKey(p => p.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DoctorPrice>()
                .HasOne(p => p.ServiceType)
                .WithMany(s => s.DoctorPrices)
                .HasForeignKey(p => p.ServiceTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.Number)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Clinic)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.ClinicId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Doctor)
                .WithMany()
                .HasForeignKey(i => i.DoctorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InvoiceLine>()
                .HasOne(l => l.Invoice)
                .WithMany(i => i.Lines)
                .HasForeignKey(l => l.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.ExpenseCategory)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.FinancialAccount)
                .WithMany()
                .HasForeignKey(e => e.FinancialAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<IncomeEntry>()
                .HasOne(e => e.Clinic)
                .WithMany(c => c.Payments)
                .HasForeignKey(e => e.ClinicId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<IncomeEntry>()
                .HasOne(e => e.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<IncomeEntry>()
                .HasOne(e => e.FinancialAccount)
                .WithMany()
                .HasForeignKey(e => e.FinancialAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LoanTransaction>()
                .HasOne(l => l.Employee)
                .WithMany()
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LoanTransaction>()
                .HasOne(l => l.FinancialAccount)
                .WithMany()
                .HasForeignKey(l => l.FinancialAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Expense>().HasIndex(e => new { e.Year, e.Month });
            modelBuilder.Entity<IncomeEntry>().HasIndex(e => new { e.Year, e.Month });
            modelBuilder.Entity<LoanTransaction>().HasIndex(e => new { e.Year, e.Month });

            modelBuilder.Seed();
        }
    }
}
