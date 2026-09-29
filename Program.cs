using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Models.Interfaces;
using DentalLab.Models.UnitOfWork;
using DentalLab.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<InvoiceService>();

builder.Services.AddDbContext<AppDbContext>(x =>
    x.UseSqlServer(builder.Configuration.GetConnectionString("DbCon")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 3;
    options.Password.RequiredUniqueChars = 1;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAutoMapper(typeof(MappingProfile));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
}
else
{
    app.UseDeveloperExceptionPage();
}

// Somee free hosting is HTTP — HTTPS redirect can break the site there.
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        // Never auto-import Excel unless explicitly enabled (Somee free DB is only ~30 MB).
        var autoImport = app.Configuration.GetValue("AutoImportExcel", false);
        if (autoImport)
        {
            var importer = scope.ServiceProvider.GetRequiredService<ExcelImportService>();

            if (!db.LabCases.Any())
                await importer.ImportAsync();

            if (!db.Expenses.Any() && !db.IncomeEntries.Any())
                await importer.ImportFinanceAsync();
        }
    }
    catch (Exception ex)
    {
        // Don't crash the whole host process before we can see logs (shared hosting 502.5).
        logger.LogError(ex, "Database migrate/startup failed. Check connection string and SQL access.");
    }
}

app.Run();
