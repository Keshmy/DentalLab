using DentalLab.Models.Entities;
using DentalLab.ViewModels.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.ViewComponents
{
    [ViewComponent(Name = "UserRoles")]
    public class UserRolesViewComponent : ViewComponent
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContext;

        public UserRolesViewComponent(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContext)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _httpContext = httpContext;
        }

        public async Task<IViewComponentResult> InvokeAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return View("Default", new List<UserRolesVM>());

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return View("Default", new List<UserRolesVM>());

            var currentUser = await _userManager.GetUserAsync(_httpContext.HttpContext!.User);
            if (currentUser == null)
                return View("Default", new List<UserRolesVM>());

            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            var list = new List<UserRolesVM>();

            foreach (var role in roles)
            {
                if (role.Name == "Prog" && !await _userManager.IsInRoleAsync(currentUser, "Prog"))
                    continue;

                list.Add(new UserRolesVM
                {
                    RoleId = role.Id,
                    RoleName = role.Name ?? "",
                    IsSelected = await _userManager.IsInRoleAsync(user, role.Name!)
                });
            }

            ViewBag.UserId = userId;
            return View(list);
        }
    }
}
