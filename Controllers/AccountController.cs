using DentalLab.Models.Entities;
using DentalLab.ViewModels.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalLab.Models.Interfaces;

namespace DentalLab.Controllers
{
    public class AccountController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUnitOfWork<UserProfile> _userProfile;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IUnitOfWork<UserProfile> userProfile,
            IWebHostEnvironment host) : base(host)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _userProfile = userProfile;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Index", "Admin");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM loginVM)
        {
            if (!ModelState.IsValid)
                return View(loginVM);

            var user = await _userManager.FindByEmailAsync(loginVM.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "البريد الإلكتروني غير موجود");
                return View(loginVM);
            }

            var result = await _signInManager.PasswordSignInAsync(user, loginVM.Password, loginVM.RememberMe, true);
            if (result.Succeeded)
            {
                user.LastAccessTime = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                return RedirectToAction("Index", "Admin");
            }

            if (result.IsLockedOut)
            {
                ViewBag.ErrorTitle = "فشل تسجيل الدخول";
                ViewBag.ErrorMessage = user.LockoutEnd.HasValue && user.LockoutEnd.Value.Year > DateTimeOffset.UtcNow.Year
                    ? $"الحساب '{loginVM.Email}' موقف من قبل المدير."
                    : $"خمس محاولات خاطئة. الحساب '{loginVM.Email}' موقف لمدة 15 دقيقة.";
                return View("Result");
            }

            ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة");
            return View(loginVM);
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [Authorize(Roles = "Prog,Admin")]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> Register(RegisterVM registerVM)
        {
            if (!ModelState.IsValid)
                return View(registerVM);

            var user = new ApplicationUser
            {
                UserName = registerVM.Email,
                Email = registerVM.Email,
                Age = registerVM.Age,
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, registerVM.Password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("User"))
                    await _roleManager.CreateAsync(new IdentityRole("User"));
                await _userManager.AddToRoleAsync(user, "User");

                ViewBag.SuccessTitle = "تم إنشاء الحساب";
                ViewBag.SuccessMessage = "يمكن للمستخدم تسجيل الدخول الآن.";
                return View("Result");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(registerVM);
        }

        [AcceptVerbs("Get", "Post")]
        public async Task<IActionResult> IsEmailInUse(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user == null ? Json(true) : Json($"البريد '{email}' مستخدم مسبقاً");
        }

        [Authorize]
        public async Task<IActionResult> UserProfile(string userId)
        {
            if (userId == null)
                return View("NotFound");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return View("NotFound");

            ViewBag.Email = user.Email;
            var userProfile = await _userProfile.Repository.GetWhere(p => p.UserId == userId).FirstOrDefaultAsync()
                ?? new UserProfile { UserId = userId };
            return View(userProfile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UserProfile(UserProfile userProfile, string gender, string? isImg1)
        {
            string? displayName = await _userProfile.Repository
                .GetWhere(u => u.DisplayName == userProfile.DisplayName && u.Id != userProfile.Id)
                .Select(n => n.DisplayName)
                .FirstOrDefaultAsync();

            if (displayName != null)
            {
                TempData["ErrorMessage"] = "الاسم الظاهر محجوز";
                return View(userProfile);
            }

            userProfile.ImageUrl = UploadFile("UsersProfile", userProfile.Image, userProfile.ImageUrl, isImg1);

            try
            {
                userProfile.Gender = Convert.ToBoolean(gender);
                if (userProfile.Id == Guid.Empty)
                    _userProfile.Repository.Insert(userProfile);
                else
                {
                    userProfile.Modified = DateTime.UtcNow;
                    _userProfile.Repository.Update(userProfile);
                }
                await _userProfile.SaveAsync();
            }
            catch
            {
                return View("Error");
            }

            TempData["SuccessMessage"] = "تم الحفظ بنجاح";
            return RedirectToAction("UserProfile", new { userId = userProfile.UserId });
        }

        [AcceptVerbs("Get", "Post")]
        public async Task<IActionResult> IsDisplayNameInUse(string displayName, Guid id)
        {
            var name = await _userProfile.Repository
                .GetWhere(u => u.DisplayName == displayName && u.Id != id)
                .Select(n => n.DisplayName)
                .FirstOrDefaultAsync();
            return name == null ? Json(true) : Json($"الاسم الظاهر '{name}' مستخدم مسبقاً");
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return View("NotFound");

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "تم تغيير كلمة المرور";
                return RedirectToAction("Index", "Admin");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        public IActionResult AccessDenied() => View();
    }
}
