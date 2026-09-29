using DentalLab.Classes;
using DentalLab.Models.Entities;
using DentalLab.Models.Interfaces;
using DentalLab.ViewModels.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PasswordGenerator;

namespace DentalLab.Controllers
{
    [Authorize(Roles = "Prog,Admin")]
    [ViewLayout("_LayoutDashboard")]
    public class UsersController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork<UserProfile> _userProfile;

        public UsersController(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IUnitOfWork<UserProfile> userProfile,
            IWebHostEnvironment host) : base(host)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _userProfile = userProfile;
        }

        public async Task<IActionResult> Index(string? userList)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return View("NotFound");

            var query = _userManager.Users.Include(u => u.UserProfile).AsNoTracking();

            bool currentIsProg = await _userManager.IsInRoleAsync(currentUser, "Prog");
            if (!currentIsProg)
            {
                var progUsers = await _userManager.GetUsersInRoleAsync("Prog");
                var progIds = progUsers.Select(u => u.Id).ToHashSet();
                query = query.Where(u => !progIds.Contains(u.Id));
            }

            var users = await query.OrderByDescending(u => u.CreatedDate).ToListAsync();

            ViewBag.AllUsers = users.Count;
            ViewBag.Confirmed = users.Count(u => u.EmailConfirmed);
            ViewBag.Unconfirmed = users.Count(u => !u.EmailConfirmed);
            ViewBag.Locked = users.Count(u => u.LockoutEnd > DateTime.UtcNow);
            ViewBag.UserList = userList;

            return userList switch
            {
                "Confirmed" => View(users.Where(u => u.EmailConfirmed)),
                "Unconfirmed" => View(users.Where(u => !u.EmailConfirmed)),
                "Locked" => View(users.Where(u => u.LockoutEnd > DateTime.UtcNow)),
                _ => View(users)
            };
        }

        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return View("NotFound");

            var user = await _userManager.Users.Include(p => p.UserProfile).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return View("NotFound");

            ViewBag.userId = user.Id;
            var userRoles = await _userManager.GetRolesAsync(user);

            return View(new EditUserVM
            {
                Id = user.Id,
                Email = user.Email ?? "",
                EmailConfirmed = user.EmailConfirmed,
                LastAccessTime = user.LastAccessTime,
                Age = user.Age,
                LockoutEnd = user.LockoutEnd,
                CreatedDate = user.CreatedDate,
                ModifiedDate = user.ModifiedDate,
                Roles = userRoles.ToList(),
                DisplayName = user.UserProfile?.DisplayName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserVM editUserVM)
        {
            if (!ModelState.IsValid)
                return RedirectToAction("EditUser", new { id = editUserVM.Id });

            var user = await _userManager.FindByIdAsync(editUserVM.Id);
            if (user == null)
                return View("NotFound");

            user.Email = editUserVM.Email;
            user.UserName = editUserVM.Email;
            user.Age = editUserVM.Age;
            user.ModifiedDate = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded ? "تم تحديث بيانات المستخدم" : string.Join("<br>", result.Errors.Select(e => e.Description));

            return RedirectToAction("EditUser", new { id = editUserVM.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockoutUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return View("NotFound");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == id)
            {
                TempData["ErrorMessage"] = "لا يمكن إيقاف الحساب الحالي";
                return RedirectToAction(nameof(EditUser), new { id });
            }

            bool locked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            var result = locked
                ? await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow)
                : await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(10));

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded ? "تم تحديث حالة الإيقاف" : string.Join("<br>", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(EditUser), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return View("NotFound");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == id)
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الحساب الحالي";
                return RedirectToAction(nameof(EditUser), new { id });
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "تم حذف المستخدم";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = string.Join("<br>", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(EditUser), new { id });
        }

        public async Task<IActionResult> CreateUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return View("NotFound");

            List<IdentityRole> roles;
            if (await _userManager.IsInRoleAsync(user, "Prog"))
            {
                roles = await _roleManager.Roles
                    .Where(r => r.Name != "Employee" && r.Name != "EmployeeRequest")
                    .OrderBy(r => r.Name)
                    .ToListAsync();
            }
            else
            {
                roles = await _roleManager.Roles
                    .Where(r => r.Name != "Prog" && r.Name != "Employee" && r.Name != "EmployeeRequest")
                    .OrderBy(r => r.Name)
                    .ToListAsync();
            }

            ViewData["Roles"] = new SelectList(roles, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Age = model.Age,
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow
            };

            var generatedPassword = new Password(true, true, true, true, 5).Next();
            var result = await _userManager.CreateAsync(user, generatedPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            var role = await _roleManager.FindByIdAsync(model.RoleId);
            if (role?.Name != null)
                await _userManager.AddToRoleAsync(user, role.Name);

            ViewBag.SuccessTitle = "تم إنشاء المستخدم";
            ViewBag.SuccessMessage = $"البريد: {model.Email} — كلمة المرور: {generatedPassword}";
            return View("Result");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserRoles(string userId, List<UserRolesVM> model)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return View("NotFound");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            var selected = model.Where(r => r.IsSelected).Select(r => r.RoleName).ToList();
            if (selected.Count > 0)
                await _userManager.AddToRolesAsync(user, selected);

            TempData["SuccessMessage"] = "تم تحديث صلاحيات المستخدم";
            return RedirectToAction(nameof(EditUser), new { id = userId });
        }
    }
}
