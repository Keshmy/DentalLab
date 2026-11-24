using DentalLab.Classes;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DentalLab.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DentalLab.Models
{
    /// <summary>
    /// يبني سطور سجل التغييرات من ChangeTracker قبل الحفظ.
    /// يُسجَّل فقط ما يهم المال والعمل: الأسعار، الحالات، الفواتير، التحصيلات، المصاريف، الأرصدة.
    /// </summary>
    public static class AuditTrail
    {
        private static readonly Dictionary<Type, string> Audited = new()
        {
            [typeof(ServiceType)] = "منتج / سعر عام",
            [typeof(DoctorPrice)] = "سعر طبيب",
            [typeof(ClinicPrice)] = "سعر عيادة",
            [typeof(LabCase)] = "حالة",
            [typeof(Invoice)] = "فاتورة",
            [typeof(IncomeEntry)] = "تحصيل / إيراد",
            [typeof(Expense)] = "مصروف",
            [typeof(ClinicCreditEntry)] = "رصيد دائن",
            [typeof(Clinic)] = "عيادة",
            [typeof(Doctor)] = "طبيب",
            [typeof(AccountTransfer)] = "تحويل بين حسابات",
        };

        private static readonly HashSet<string> Skipped =
            ["Id", "Created", "Modified", "Year", "Month", "DocumentNumber", "ReceiptNumber", "ImportBatchId"];

        public static IEnumerable<string> AuditedTypeNames => Audited.Keys.Select(t => t.Name);

        public static string LabelFor(string entityType) =>
            Audited.FirstOrDefault(kv => kv.Key.Name == entityType).Value ?? entityType;

        public static async Task<List<AuditLog>> CollectAsync(AppDbContext db, string? userName, CancellationToken ct = default)
        {
            var entries = db.ChangeTracker.Entries()
                .Where(e => Audited.ContainsKey(e.Entity.GetType())
                            && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            if (entries.Count == 0)
                return [];

            var names = new NameLookup(db);
            var logs = new List<AuditLog>();

            foreach (var entry in entries)
            {
                EnsureKey(entry);

                var log = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    UserName = userName,
                    EntityType = entry.Entity.GetType().Name,
                    EntityLabel = Audited[entry.Entity.GetType()],
                    EntityId = entry.Property("Id").CurrentValue?.ToString()
                };

                var lines = new List<string>();

                switch (entry.State)
                {
                    case EntityState.Added:
                        log.Action = AuditAction.Added;
                        foreach (var p in entry.Properties.Where(p => !Skipped.Contains(p.Metadata.Name)))
                        {
                            if (IsEmpty(p.CurrentValue)) continue;
                            lines.Add($"{Label(entry, p.Metadata.Name)}: {await names.FormatAsync(p.Metadata.Name, p.CurrentValue, ct)}");
                        }
                        if (entry.Entity is LabCase added && added.Items.Count > 0)
                            lines.Add("الأسطر: " + DescribeItems(added.Items));
                        break;

                    case EntityState.Deleted:
                        log.Action = AuditAction.Deleted;
                        foreach (var p in entry.Properties.Where(p => !Skipped.Contains(p.Metadata.Name)))
                        {
                            if (IsEmpty(p.OriginalValue)) continue;
                            lines.Add($"{Label(entry, p.Metadata.Name)}: {await names.FormatAsync(p.Metadata.Name, p.OriginalValue, ct)}");
                        }
                        break;

                    case EntityState.Modified:
                        log.Action = AuditAction.Modified;
                        // الكيانات المعدَّلة عبر Update() لا تحمل القيم الأصلية — نقرأها من قاعدة البيانات.
                        var dbValues = await entry.GetDatabaseValuesAsync(ct);
                        foreach (var p in entry.Properties.Where(p => !Skipped.Contains(p.Metadata.Name)))
                        {
                            var before = dbValues != null ? dbValues[p.Metadata] : p.OriginalValue;
                            if (Equals(before, p.CurrentValue)) continue;
                            lines.Add($"{Label(entry, p.Metadata.Name)}: " +
                                      $"{await names.FormatAsync(p.Metadata.Name, before, ct)} ← {await names.FormatAsync(p.Metadata.Name, p.CurrentValue, ct)}");
                        }
                        if (lines.Count == 0)
                            continue; // حفظ بدون تغيير فعلي
                        break;
                }

                log.Summary = Truncate(await SummarizeAsync(entry, names, ct), 300);
                log.Changes = lines.Count == 0 ? null : string.Join("\n", lines);
                logs.Add(log);
            }

            return logs;
        }

        /// <summary>وصف الأسطر مرتباً أبجدياً حتى لا يظهر «تغيير» لمجرد اختلاف الترتيب.</summary>
        public static string DescribeItems(IEnumerable<LabCaseItem> items) =>
            string.Join("، ", items
                .Select(i => $"{i.DisplayName} ×{i.Quantity} @ {i.UnitPrice:N2}{(i.IsRemake ? " (إعادة)" : "")}" +
                             (string.IsNullOrWhiteSpace(i.Teeth) ? "" : $" [أسنان {TeethCode.ToDisplay(i.Teeth)}]"))
                .OrderBy(s => s, StringComparer.Ordinal));

        /// <summary>المعرّفات المولّدة في قاعدة البيانات لا تُعرف قبل الحفظ؛ نولّدها هنا ليرتبط السجل بها.</summary>
        private static void EnsureKey(EntityEntry entry)
        {
            if (entry.State != EntityState.Added) return;
            var key = entry.Property("Id");
            if (key.CurrentValue is Guid g && (g == Guid.Empty || key.IsTemporary))
            {
                key.CurrentValue = Guid.NewGuid();
                key.IsTemporary = false;
            }
        }

        private static async Task<string> SummarizeAsync(EntityEntry entry, NameLookup names, CancellationToken ct)
        {
            object? V(string prop)
            {
                var p = entry.Property(prop);
                return entry.State == EntityState.Deleted ? p.OriginalValue : p.CurrentValue;
            }

            return entry.Entity switch
            {
                LabCase => $"حالة رقم {V("CaseNumber")} — {V("PatientName")}",
                Invoice => $"فاتورة رقم {V("Number")}",
                DoctorPrice => $"{await names.FormatAsync("DoctorId", V("DoctorId"), ct)} — {await names.FormatAsync("ServiceTypeId", V("ServiceTypeId"), ct)}",
                ClinicPrice => $"{await names.FormatAsync("ClinicId", V("ClinicId"), ct)} — {await names.FormatAsync("ServiceTypeId", V("ServiceTypeId"), ct)}",
                ServiceType or Clinic or Doctor => V("Name")?.ToString() ?? "",
                IncomeEntry or Expense => $"{V("Description")} — {Format(V("Amount"))}",
                ClinicCreditEntry => $"{await names.FormatAsync("ClinicId", V("ClinicId"), ct)} — {Format(V("Amount"))}",
                AccountTransfer => $"من {await names.FormatAsync("FromAccountId", V("FromAccountId"), ct)} إلى {await names.FormatAsync("ToAccountId", V("ToAccountId"), ct)} — {Format(V("Amount"))}",
                _ => entry.Entity.GetType().Name
            };
        }

        private static string Label(EntityEntry entry, string property)
        {
            var info = entry.Entity.GetType().GetProperty(property);
            return info?.GetCustomAttribute<DisplayAttribute>()?.Name ?? property;
        }

        private static bool IsEmpty(object? v) => v switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            Guid g => g == Guid.Empty,
            _ => false
        };

        internal static string Format(object? v) => v switch
        {
            null => "—",
            decimal d => d.ToString("N2"),
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd/MM/yyyy") : dt.ToString("dd/MM/yyyy HH:mm"),
            bool b => b ? "نعم" : "لا",
            string s when string.IsNullOrWhiteSpace(s) => "—",
            _ => v.ToString() ?? "—"
        };

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        /// <summary>يحوّل المعرّفات (طبيب/عيادة/منتج/حساب...) إلى أسماء مقروءة، مع ذاكرة مؤقتة لكل عملية حفظ.</summary>
        private sealed class NameLookup(AppDbContext db)
        {
            private readonly Dictionary<(string, Guid), string> _cache = [];

            public async Task<string> FormatAsync(string property, object? value, CancellationToken ct)
            {
                if (value is not Guid id || id == Guid.Empty)
                    return Format(value);

                if (_cache.TryGetValue((property, id), out var cached))
                    return cached;

                string? name = property switch
                {
                    "DoctorId" => await db.Doctors.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).FirstOrDefaultAsync(ct),
                    "ClinicId" => await db.Clinics.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).FirstOrDefaultAsync(ct),
                    "ServiceTypeId" => await db.ServiceTypes.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).FirstOrDefaultAsync(ct),
                    "ExpenseCategoryId" => await db.ExpenseCategories.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).FirstOrDefaultAsync(ct),
                    "FinancialAccountId" or "FromAccountId" or "ToAccountId" => await db.FinancialAccounts.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).FirstOrDefaultAsync(ct),
                    "InvoiceId" => await db.Invoices.AsNoTracking().Where(x => x.Id == id).Select(x => "فاتورة " + x.Number).FirstOrDefaultAsync(ct),
                    "LabCaseId" => await db.LabCases.AsNoTracking().Where(x => x.Id == id).Select(x => "حالة " + x.CaseNumber + " — " + x.PatientName).FirstOrDefaultAsync(ct),
                    _ => null
                };

                // قد يكون السجل جديداً في نفس عملية الحفظ (لم يُكتب بعد) — نبحث في المتتبَّع محلياً.
                name ??= db.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added && e.Metadata.FindProperty("Id") != null
                                && Equals(e.Property("Id").CurrentValue, id) && e.Metadata.FindProperty("Name") != null)
                    .Select(e => e.Property("Name").CurrentValue?.ToString())
                    .FirstOrDefault();

                var result = name ?? id.ToString()[..8];
                _cache[(property, id)] = result;
                return result;
            }
        }
    }
}
