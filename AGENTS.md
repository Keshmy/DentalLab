# Agents / AI — start here

Before editing this repo, **read [`DEVELOPER.md`](DEVELOPER.md)** end to end.

## Hard rules

- Work on branch **`development`** unless told otherwise.
- Stack is **ASP.NET Core 9 MVC + EF Core + SQL Server + Identity**. Do not switch frameworks.
- UI is **Arabic RTL**; keep existing dashboard patterns.
- Pricing order: **Clinic → Doctor → Default service price** (`PricingService`).
- Lab cases require **doctor + clinic**.
- Migrations apply on startup; add a new migration when the model changes.
- Never commit `bin/`, `obj/`, `publish/`, `*.zip`, or `wwwroot/upload/`.
- Do not weaken `[Authorize]` / role checks.
- Prefer small, focused diffs that match existing code style.

## Quick start

```bash
git checkout development
dotnet restore
dotnet run --urls http://localhost:5141
```

Login: `Programmer@Gmail.com` / `111`
