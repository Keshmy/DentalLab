# DentalLab — Developer & AI Onboarding Guide

Use this file to start coding manually or with any AI assistant.  
Product name (UI): **مختبر حد السيف**. UI language: **Arabic RTL**.

---

## 1. What this project is

ASP.NET Core **MVC** app for a dental laboratory:

- Lab cases (حالات) with line items, teeth, remakes
- Doctors / clinics and layered pricing
- Monthly invoices and cash ledger
- Finance (expenses, income, loans, receivables, P&L)
- Identity (users, roles, employees)
- Site settings + optional Excel import
- Backup / restore admin page (works best on a Windows machine/VPS with local SQL; often blocked on shared hosting)

Repo: https://github.com/Keshmy/DentalLab

---

## 2. Tech stack

| Layer | Choice |
|-------|--------|
| Runtime | .NET 9 |
| UI | ASP.NET Core MVC + Razor (RTL Bootstrap-based dashboard) |
| Data | EF Core 9 + SQL Server |
| Auth | ASP.NET Core Identity |
| Mapping | AutoMapper |
| Excel | ClosedXML |

---

## 3. Branches (how we share work)

| Branch | Use for |
|--------|---------|
| `development` | Default day-to-day coding (start here) |
| `staging` | Pre-release testing |
| `production` | Stable / deployable |

```bash
git clone https://github.com/Keshmy/DentalLab.git
cd DentalLab
git checkout development
```

Workflow suggestion: feature work on `development` → merge to `staging` for test → merge to `production` when ready.

---

## 4. Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server (LocalDB, Express, or full instance)
- Optional: Visual Studio 2022 / Rider / VS Code + C# extension

---

## 5. First-time setup (human or AI)

1. Clone and checkout `development` (see above).
2. Edit connection string in `appsettings.json` (and/or `appsettings.Development.json`):

```json
"ConnectionStrings": {
  "DbCon": "Server=.;Database=DentalLabDb_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```

3. Restore and run:

```bash
dotnet restore
dotnet run --urls http://localhost:5141
```

4. Open **http://localhost:5141**

Migrations run automatically on startup (`Program.cs` → `db.Database.Migrate()`).

### Default login (seeded)

| Field | Value |
|-------|--------|
| Email | `Programmer@Gmail.com` |
| Password | `111` |

Roles include: `Prog`, `Admin`, `Employee`, `EmployeeRequest`.

---

## 6. Project map

```
DentalLab/
├── Controllers/       # HTTP endpoints (MVC)
├── Models/
│   ├── Entities/      # Domain entities
│   ├── AppDbContext.cs
│   ├── Repositories/  # Generic repository
│   └── UnitOfWork/
├── ViewModels/        # Form / page models
├── Views/             # Razor (Arabic RTL)
├── Services/          # Business logic (pricing, invoices, finance, backup, excel)
├── Migrations/        # EF migrations — do not edit applied ones casually
├── wwwroot/           # Static files; uploads go under wwwroot/upload
├── Data/              # Optional Archive.xlsx for import
├── Program.cs         # DI, pipeline, migrate-on-startup
├── appsettings.json
├── appsettings.Development.json
└── appsettings.Production.json
```

### Important services

| Service | Responsibility |
|---------|----------------|
| `PricingService` | Resolve unit price for a case line |
| `InvoiceService` | Monthly invoices |
| `FinanceService` | Finance summaries / reports |
| `ExcelImportService` | Archive / finance Excel import |
| `BackupService` | SQL backup/restore + uploads zip |
| `AutoBackupHostedService` | Daily scheduled backup |

---

## 7. Domain rules (do not break)

### Pricing priority (highest → lowest)

1. **Clinic special price** (`ClinicPrices`) when the case has a clinic  
2. **Doctor special price** (`DoctorPrices`)  
3. **Default** `ServiceType` price  

Implementation: `Services/PricingService.cs`.

### Lab cases

- Doctor **and** clinic are required on create/edit.
- Case lines can mark **remake** (`IsRemake`). Remake pricing/counting is special-cased in pricing/finance.
- Tooth arch: Upper / Lower per line where applicable (`ToothArch`).
- Do not soft-delete cases unless product owners change that decision (current code uses hard delete when allowed).
- Do not delete a doctor/clinic that still has related cases (controllers guard this).

### Invoices

- Block collecting more than the remaining balance (over-payment guard on invoice details / payments).

### Backup / restore

- UI: Admin → النسخ الاحتياطي (`Views/Admin/Backup.cshtml`).
- Backup writes dated folders (never overwrite) with `database.bak` + optional `uploads.zip`.
- Restore runs **on the server** (SQL `BACKUP` / `RESTORE`). Shared hosts (Somee, shared Windows) often **block** this. Prefer VPS / local SQL for real backup-restore.
- Keep `AutoImportExcel` **false** unless intentionally importing (small free DBs fill quickly).

### UI / UX conventions

- Keep Arabic labels and RTL layout.
- Prefer existing dashboard layout: `Views/Shared/_LayoutDashboard.cshtml`.
- Site logo/name come from `SiteInfo` settings — wire new branding through that, not hardcoded.

---

## 8. How to add a typical feature

1. Entity in `Models/Entities/` + register `DbSet` in `AppDbContext`.
2. Create EF migration:
   ```bash
   dotnet ef migrations add YourFeatureName
   ```
3. ViewModel in `ViewModels/`.
4. Logic in `Services/` when non-trivial (keep controllers thin).
5. Controller + Razor views under matching folders.
6. Authorize with roles, e.g. `[Authorize(Roles = "Prog,Admin")]`.
7. Add nav link in `_LayoutDashboard.cshtml` if needed.
8. Run locally, then commit to `development`.

### Migrations tips

- Prefer new migrations over editing old ones that may already be applied on shared DBs.
- Startup already applies pending migrations — no manual `update-database` required for local run if SQL is reachable.

---

## 9. Coding conventions

- Match existing style: Controllers → Services → EF/`AppDbContext`.
- Use ViewModels for forms; avoid binding entities directly when validation/UI differs.
- Preserve Arabic user-facing strings.
- Do not commit:
  - `bin/`, `obj/`, `publish/`, `*.zip`, `wwwroot/upload/`
  - Secrets / real production passwords (use local `appsettings.Development.json` overrides)
- `.gitignore` already excludes build output and uploads.

---

## 10. Useful commands

```bash
# Run
dotnet run --urls http://localhost:5141

# Build
dotnet build

# New migration
dotnet ef migrations add NameOfChange

# Publish for Somee-like 32-bit IIS (self-contained)
dotnet publish -c Release -r win-x86 --self-contained true -o ./publish
```

---

## 11. Hosting notes

| Host | Notes |
|------|--------|
| Local / Windows VPS | Full features including SQL backup/restore if SQL can write the backup folder |
| Somee free / shared | App can run; SQL BACKUP/RESTORE usually fails; watch storage (~150 MB); prefer Upload **archive** (auto-extract) |
| Libyan Spider shared | Same shared-SQL limits as Somee |
| Libyan Spider Windows VPS | Good fit for backup/restore if SQL is on the same machine |

Production connection string: `appsettings.Production.json` → `ConnectionStrings:DbCon`.

---

## 12. Checklist for AI assistants

Before changing code:

1. Read this file and skim `Program.cs`, `Models/AppDbContext.cs`, and the relevant controller/service.
2. Prefer smallest change that solves the request; match existing patterns.
3. Do not invent a new frontend stack (no React/Blazor rewrite unless asked).
4. Do not weaken auth / role checks.
5. Keep pricing priority and case validation rules intact unless the user explicitly changes them.
6. After substantive model changes, add an EF migration.
7. Do not commit `bin/`, `obj/`, publish zips, or upload folders.
8. Default work branch: **`development`**.

---

## 13. Where to look first (by task)

| Task | Start here |
|------|------------|
| Case form / remake / teeth | `Controllers/LabCasesController.cs`, `Views/LabCases/_CaseForm*.cshtml` |
| Prices | `Services/PricingService.cs`, `DoctorPricesController`, `ClinicPricesController` |
| Invoices | `Services/InvoiceService.cs`, `Controllers/InvoicesController.cs` |
| Finance | `Services/FinanceService.cs`, `Controllers/FinanceController.cs` |
| Backup | `Services/BackupService.cs`, `Controllers/AdminController.cs`, `Views/Admin/Backup.cshtml` |
| Login / users | `Controllers/AccountController.cs`, Identity entities |
| Menu / branding | `Views/Shared/_LayoutDashboard.cshtml`, `SiteInfo` |

---

## 14. Need help?

- App README: [`README.md`](README.md)
- Issues / questions: coordinate on the GitHub repo with the team
