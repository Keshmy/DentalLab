using DentalLab.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Models
{
    public static class ModelBuilderExtensions
    {
        public static void Seed(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SiteInfo>().HasData(new SiteInfo
            {
                Id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
                Name = "مختبر حد السيف",
                Activity = "مختبر أسنان — زركونيا وتيجان وجسور",
                About = "مختبر حد السيف لصناعة الأسنان: تسجيل الحالات، الأطباء، العيادات، الأسعار، وسجل الصندوق.",
                Phone = "091-9883982",
                BankAccountName = "محمد نجم الدين الطاهر الفتحي",
                BankName = "مصرف شمال أفريقيا",
                BankAccountNumber = "LY07007014014011428015012",
                Created = new DateTime(2026, 1, 1)
            });

            var progRoleId = "2cdc7bbd-449b-4fa5-87c7-4d8cf0683bea";
            var adminRoleId = "7a1c9e2f-3b44-4d11-9c88-21f0a1b2c3d4";
            var employeeRoleId = "8b2d0f3a-4c55-4e22-8d99-32a1b2c3d4e5";
            var employeeRequestRoleId = "9c3e1a4b-5d66-4f33-8eaa-43b2c3d4e5f6";
            var programmerId = "a1b495e6-dcb6-4763-9994-a4d74b93105c";
            var concurrencyStampRole = "a1404a92-7520-4989-8c46-5922377cb2d0";
            var concurrencyStampUser = "f67b0714-171b-4b5f-a459-817a40f3041d";
            var securityStamp = "1d4afc4a-ef8a-4e5e-a09a-7ee026455e41";

            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = progRoleId,
                    Name = "Prog",
                    NormalizedName = "PROG",
                    ConcurrencyStamp = concurrencyStampRole
                },
                new IdentityRole
                {
                    Id = adminRoleId,
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = "b2504a92-7520-4989-8c46-5922377cb2d1"
                },
                new IdentityRole
                {
                    Id = employeeRoleId,
                    Name = "Employee",
                    NormalizedName = "EMPLOYEE",
                    ConcurrencyStamp = "c3604a92-7520-4989-8c46-5922377cb2d2"
                },
                new IdentityRole
                {
                    Id = employeeRequestRoleId,
                    Name = "EmployeeRequest",
                    NormalizedName = "EMPLOYEEREQUEST",
                    ConcurrencyStamp = "d4704a92-7520-4989-8c46-5922377cb2d3"
                }
            );

            const string programmerName = "Programmer@Gmail.com";
            modelBuilder.Entity<ApplicationUser>().HasData(new ApplicationUser
            {
                Id = programmerId,
                UserName = programmerName,
                NormalizedUserName = programmerName.ToUpper(),
                Email = programmerName,
                NormalizedEmail = programmerName.ToUpper(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                LockoutEnabled = true,
                SecurityStamp = securityStamp,
                ConcurrencyStamp = concurrencyStampUser,
                CreatedDate = new DateTime(2025, 1, 1),
                Age = 30,
                Approval = true,
                PasswordHash = "AQAAAAIAAYagAAAAENggG9+6Z01XNeB9YmF/XmQybN3d/MpCrkCkJ58k03l2udJQx0IaIujsHHxHRpulzQ=="
            });

            modelBuilder.Entity<IdentityUserRole<string>>().HasData(
                new IdentityUserRole<string>
                {
                    RoleId = progRoleId,
                    UserId = programmerId
                });

            SeedFinance(modelBuilder);
        }

        private static void SeedFinance(ModelBuilder modelBuilder)
        {
            var seedDate = new DateTime(2026, 1, 1);

            // بنود المصاريف — الأسماء مأخوذة من ملف الإيرادات والمصاريف
            var categories = new (string Id, string Name, bool IsCapital)[]
            {
                ("1a000000-0000-0000-0000-000000000001", ExpenseCategoryNames.Materials, false),
                ("1a000000-0000-0000-0000-000000000002", ExpenseCategoryNames.Rent, false),
                ("1a000000-0000-0000-0000-000000000003", ExpenseCategoryNames.Salaries, false),
                ("1a000000-0000-0000-0000-000000000004", ExpenseCategoryNames.Maintenance, false),
                ("1a000000-0000-0000-0000-000000000005", ExpenseCategoryNames.BankFees, false),
                ("1a000000-0000-0000-0000-000000000006", ExpenseCategoryNames.OutsourcedLab, false),
                ("1a000000-0000-0000-0000-000000000007", ExpenseCategoryNames.Transport, false),
                ("1a000000-0000-0000-0000-000000000008", ExpenseCategoryNames.Utilities, false),
                ("1a000000-0000-0000-0000-000000000009", ExpenseCategoryNames.PartnerShare, false),
                ("1a000000-0000-0000-0000-00000000000a", ExpenseCategoryNames.Equipment, true),
                ("1a000000-0000-0000-0000-00000000000b", ExpenseCategoryNames.Other, false)
            };

            foreach (var (id, name, isCapital) in categories)
            {
                modelBuilder.Entity<ExpenseCategory>().HasData(new ExpenseCategory
                {
                    Id = Guid.Parse(id),
                    Name = name,
                    IsCapital = isCapital,
                    Created = seedDate
                });
            }

            // الحسابات الموجودة فعلاً في ملف الإكسل
            var accounts = new (string Id, string Name, string Kind)[]
            {
                ("2b000000-0000-0000-0000-000000000001", "كاش المعمل", AccountKind.Cash),
                ("2b000000-0000-0000-0000-000000000002", "حساب عبدو", AccountKind.Bank),
                ("2b000000-0000-0000-0000-000000000003", "حساب عادل", AccountKind.Bank),
                ("2b000000-0000-0000-0000-000000000004", "حساب شمال أفريقيا", AccountKind.Bank)
            };

            foreach (var (id, name, kind) in accounts)
            {
                modelBuilder.Entity<FinancialAccount>().HasData(new FinancialAccount
                {
                    Id = Guid.Parse(id),
                    Name = name,
                    Kind = kind,
                    IsActive = true,
                    Created = seedDate
                });
            }
        }
    }
}
