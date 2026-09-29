using DentalLab.Classes;
using DentalLab.Models.Entities;
using DentalLab.ViewModels.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Controllers
{
    [Authorize(Roles = "Prog,Admin")]
    [ViewLayout("_LayoutDashboard")]
    public class RolesController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RolesController(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment host) : base(host)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? roleInUse)
        {
            ViewBag.RoleInUse = roleInUse;
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return View("NotFound");

            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            if (!await _userManager.IsInRoleAsync(user, "Prog"))
                roles = roles.Where(r => r.Name != "Prog").ToList();

            var rolesVM = new List<RoleVM>();
            foreach (var role in roles)
            {
                rolesVM.Add(new RoleVM
                {
                    Id = role.Id,
                    Name = role.Name ?? "",
                    UsersCount = (await _userManager.GetUsersInRoleAsync(role.Name!)).Count
                });
            }

            return View(rolesVM);
        }

        [Authorize(Roles = "Prog")]
        public IActionResult CreateRole() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> CreateRole(CreateRoleVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _roleManager.CreateAsync(new IdentityRole
            {
                Name = model.Name,
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"تم إنشاء الصلاحية '{model.Name}'";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> EditRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return View("NotFound");

            return View(new EditRoleVM { Id = role.Id, RoleName = role.Name ?? "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> EditRole(EditRoleVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var role = await _roleManager.FindByIdAsync(model.Id);
            if (role == null)
                return View("NotFound");

            role.Name = model.RoleName;
            var result = await _roleManager.UpdateAsync(role);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "تم تحديث الصلاحية";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "الصلاحية غير موجودة";
                return RedirectToAction(nameof(Index));
            }

            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            if (usersInRole.Any())
            {
                TempData["ErrorMessage"] = $"لا يمكن حذف '{role.Name}' لأنها مستخدمة";
                return RedirectToAction("Index", new { roleInUse = role.Name });
            }

            await _roleManager.DeleteAsync(role);
            TempData["SuccessMessage"] = "تم حذف الصلاحية";
            return RedirectToAction(nameof(Index));
        }
    }
}
