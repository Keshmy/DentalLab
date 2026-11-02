using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    /// <summary>سجل التغييرات: من عدّل الأسعار والحالات والتحصيلات ومتى. للعرض فقط.</summary>
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class AuditLogsController : Controller
    {
        private const int PageSize = 100;
        private readonly AppDbContext _context;

        public AuditLogsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? user, string? entityType, string? op,
            string? search, string? entityId, int page = 1)
        {
            var query = Filter(from, to, user, entityType, op, search, entityId);

            var total = await query.CountAsync();
            page = Math.Max(1, page);

            var rows = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.User = user;
            ViewBag.Search = search;
            ViewBag.EntityId = entityId;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
            ViewBag.Total = total;
            ViewBag.Types = new SelectList(
                AuditTrail.AuditedTypeNames.Select(t => new { Value = t, Text = AuditTrail.LabelFor(t) }),
                "Value", "Text", entityType);
            ViewBag.Actions = new SelectList(AuditAction.All, op);
            ViewBag.Users = new SelectList(
                await _context.AuditLogs.AsNoTracking().Where(a => a.UserName != null)
                    .Select(a => a.UserName!).Distinct().OrderBy(u => u).ToListAsync(),
                user);

            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> Export(DateTime? from, DateTime? to, string? user, string? entityType, string? op,
            string? search, string? entityId, string? format, [FromServices] ExportService export)
        {
            var rows = await Filter(from, to, user, entityType, op, search, entityId)
                .OrderByDescending(a => a.Timestamp)
                .Take(5000)
                .ToListAsync();

            var report = new ReportTable
            {
                Title = "سجل التغييرات",
                Subtitle = from.HasValue || to.HasValue
                    ? $"من {from:dd/MM/yyyy} إلى {(to ?? DateTime.Today):dd/MM/yyyy}"
                    : null,
                FileName = $"ChangeLog-{DateTime.Now:yyyy-MM-dd}",
                Headers = ["الوقت", "المستخدم", "العملية", "النوع", "السجل", "التغييرات"],
                ColumnWidths = [1.3f, 1.6f, 0.8f, 1.1f, 2.2f, 5],
                Rows = rows.Select(a => new object?[]
                {
                    a.Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), a.UserName ?? "النظام", a.Action, a.EntityLabel, a.Summary, a.Changes
                }).ToList(),
                Note = rows.Count == 5000 ? "عُرض أحدث 5000 سجل فقط — ضيّق الفلاتر لعرض الباقي." : null
            };

            var (content, type, name) = await export.ExportAsync(report, ExportService.ParseFormat(format));
            return File(content, type, name);
        }

        private IQueryable<AuditLog> Filter(DateTime? from, DateTime? to, string? user, string? entityType, string? op,
            string? search, string? entityId)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            // التواريخ تُدخل بالتوقيت المحلي وتُخزَّن بـ UTC.
            if (from.HasValue)
            {
                var f = from.Value.Date.ToUniversalTime();
                query = query.Where(a => a.Timestamp >= f);
            }
            if (to.HasValue)
            {
                var t = to.Value.Date.AddDays(1).ToUniversalTime();
                query = query.Where(a => a.Timestamp < t);
            }
            if (!string.IsNullOrWhiteSpace(user))
                query = query.Where(a => a.UserName == user);
            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(op))
                query = query.Where(a => a.Action == op);
            if (!string.IsNullOrWhiteSpace(entityId))
                query = query.Where(a => a.EntityId == entityId);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => (a.Summary != null && a.Summary.Contains(search))
                                         || (a.Changes != null && a.Changes.Contains(search)));

            return query;
        }
    }
}
