using DentalLab.Classes;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.Models.Interfaces;
using DentalLab.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordGenerator;

namespace DentalLab.Controllers
{
    [Authorize(Roles = "Prog,Admin")]
    [ViewLayout("_LayoutDashboard")]
    public class EmployeesController : BaseController
    {
        private readonly IUnitOfWork<Employee> _employee;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public EmployeesController(
            IUnitOfWork<Employee> employee,
            IWebHostEnvironment host,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context) : base(host)
        {
            _employee = employee;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var employees = await _employee.Repository
                .GetAll()
                .Include(e => e.ApplicationUser)
                .OrderByDescending(u => u.Created)
                .ToListAsync();
            return View(employees);
        }

        [HttpGet]
        public IActionResult CreateEmployee() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using var transaction = await _context.Database.BeginTransactionAsync();

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Age = model.Age,
                Approval = false,
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow
            };

            var generatedPassword = new Password(true, true, true, false, 5).Next();
            var result = await _userManager.CreateAsync(user, generatedPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            var employee = new Employee
            {
                Name = model.Employee.Name,
                WorkPhone = model.Employee.WorkPhone,
                Address = model.Employee.Address,
                YearsOfExperience = model.Employee.YearsOfExperience,
                Specialization = model.Employee.Specialization,
                Bio = model.Employee.Bio,
                UserId = user.Id,
                Created = DateTime.UtcNow
            };

            _employee.Repository.Insert(employee);
            await _employee.SaveAsync();

            const string requestRole = "EmployeeRequest";
            if (!await _roleManager.RoleExistsAsync(requestRole))
                await _roleManager.CreateAsync(new IdentityRole(requestRole));
            await _userManager.AddToRoleAsync(user, requestRole);

            await transaction.CommitAsync();

            ViewBag.SuccessTitle = "تم إنشاء الموظف";
            ViewBag.SuccessMessage = $"البريد: {model.Email} — كلمة المرور: {generatedPassword}. يحتاج موافقة المدير للتفعيل.";
            return View("Result");
        }

        public async Task<IActionResult> EditEmployee(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var employee = await _employee.Repository
                .GetWhere(e => e.Id == id)
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync();

            if (employee == null)
                return View("NotFound");

            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee([Bind("Id,Name,WorkPhone,Address,YearsOfExperience,Specialization,Bio,UserId,Created")] Employee employee)
        {
            var applicationUser = await _userManager.FindByIdAsync(employee.UserId ?? "");
            if (applicationUser == null)
                return View("NotFound");
            employee.ApplicationUser = applicationUser;

            if (!ModelState.IsValid)
                return View(employee);

            employee.Modified = DateTime.UtcNow;
            _employee.Repository.Update(employee);
            await _employee.SaveAsync();
            TempData["SuccessMessage"] = "تم الحفظ بنجاح";
            return RedirectToAction(nameof(EditEmployee), new { id = employee.Id });
        }

        public async Task<IActionResult> ChangeEmployeeApproval(string userId)
        {
            var user = await _userManager.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return View("NotFound");

            user.Approval = !(user.Approval ?? false);
            await _userManager.UpdateAsync(user);

            if (user.Approval == true)
            {
                if (!await _roleManager.RoleExistsAsync("Employee"))
                    await _roleManager.CreateAsync(new IdentityRole("Employee"));
                if (!await _userManager.IsInRoleAsync(user, "Employee"))
                    await _userManager.AddToRoleAsync(user, "Employee");
                if (await _userManager.IsInRoleAsync(user, "EmployeeRequest"))
                    await _userManager.RemoveFromRoleAsync(user, "EmployeeRequest");
                TempData["SuccessMessage"] = "تمت الموافقة على الموظف";
            }
            else
            {
                if (!await _roleManager.RoleExistsAsync("EmployeeRequest"))
                    await _roleManager.CreateAsync(new IdentityRole("EmployeeRequest"));
                if (!await _userManager.IsInRoleAsync(user, "EmployeeRequest"))
                    await _userManager.AddToRoleAsync(user, "EmployeeRequest");
                if (await _userManager.IsInRoleAsync(user, "Employee"))
                    await _userManager.RemoveFromRoleAsync(user, "Employee");
                TempData["SuccessMessage"] = "تم إلغاء موافقة الموظف";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
