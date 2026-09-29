# DentalLab — مختبر حد السيف

ASP.NET Core MVC dental laboratory management system with Arabic RTL UI. Track lab cases, doctor/clinic pricing, cash ledger, monthly invoices, and finance (expenses, income, loans, receivables, P&L).

## Tech stack

- **ASP.NET Core 9** MVC
- **Entity Framework Core** + SQL Server
- **ASP.NET Core Identity** (users, roles, employees)
- AutoMapper, ClosedXML (Excel import)

## Features

| Area | What it covers |
|------|----------------|
| **الحالات** | Lab cases with multi-line items and remake tracking |
| **سجل الصندوق** | Monthly cash ledger |
| **الأطباء / العيادات** | Doctors and clinics |
| **المنتجات والأسعار** | Service types and default prices |
| **أسعار الأطباء** | Per-doctor custom pricing |
| **الفواتير** | Monthly invoices (printable) |
| **المالية** | P&L, income, expenses, loans, accounts, receivables, remakes |
| **المستخدمون** | Users, roles, employees |
| **الإعدادات** | Lab profile + Excel archive / finance import |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server (LocalDB, Express, or full)

## Getting started

1. Clone the repository:

```bash
git clone https://github.com/Keshmy/DentalLab.git
cd DentalLab
```

2. Set the connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "DbCon": "Server=.;Database=DentalLabDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```

3. Run the app (migrations apply on startup):

```bash
dotnet restore
dotnet run
```

4. Open **http://localhost:5141**

### Default login

| Field | Value |
|-------|--------|
| Email | `Programmer@Gmail.com` |
| Password | `111` |

Change this password after first login in production.

## Configuration

| Setting | File | Notes |
|---------|------|--------|
| `ConnectionStrings:DbCon` | `appsettings.json` | Local / development database |
| `ConnectionStrings:DbCon` | `appsettings.Production.json` | Production hosting (e.g. Somee) |
| `AutoImportExcel` | `appsettings*.json` | Default `false`. Set `true` only when you intentionally want Excel auto-import on startup (can fill a small free DB quickly). |

Manual Excel import is available under **الإعدادات** in the dashboard when you need it.

## Project structure

```
DentalLab/
├── Controllers/          # MVC controllers (cases, finance, identity, …)
├── Models/               # Entities, EF DbContext, repositories, Unit of Work
├── Services/             # Pricing, finance, invoices, Excel import
├── Views/                # Arabic RTL Razor views + dashboard layout
├── wwwroot/              # CSS, JS, images, uploads
├── Data/                 # Optional Excel archive for import
├── Migrations/           # EF Core migrations
├── Program.cs
└── appsettings.json
```

## Publish (IIS / shared hosting)

Framework-dependent:

```bash
dotnet publish -c Release -o ./publish
```

Self-contained (useful on hosts with a fixed app-pool bitness, e.g. 32-bit):

```bash
dotnet publish -c Release -r win-x86 --self-contained true -o ./publish
```

Upload the contents of `publish` to the site root (include `web.config`). Ensure a `logs` folder exists if stdout logging is enabled.

## Roles

| Role | Purpose |
|------|---------|
| `Prog` | Full access (seeded programmer account) |
| `Admin` | Admin / finance access |
| `Employee` | Day-to-day lab work |
| `EmployeeRequest` | Limited / request workflow |

## License

Private project for مختبر حد السيف unless otherwise stated.
