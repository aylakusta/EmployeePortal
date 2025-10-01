using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Areas.Admin.ViewModels;
using WebUI.Models;
using System.Globalization;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DashboardController(ApplicationDbContext ctx, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _ctx = ctx;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var empCount = await _ctx.Employees.CountAsync();
            var depCount = await _ctx.Departments.CountAsync();
            var annCount = await _ctx.Announcements.CountAsync();
            var srvCount = await _ctx.Services.CountAsync();

            var tr = CultureInfo.GetCultureInfo("tr-TR");

            var vm = new DashboardViewModel
            {
                Kpis = new List<DashboardKpi>
                {
                    new DashboardKpi { Title = "Çalışan",   Value = empCount.ToString("N0", tr), Icon = "bi-people",     Url="/Admin/Employees" },
                    new DashboardKpi { Title = "Departman", Value = depCount.ToString("N0", tr), Icon = "bi-diagram-3", Url="/Admin/Departments" },
                    new DashboardKpi { Title = "Duyuru",    Value = annCount.ToString("N0", tr), Icon = "bi-megaphone", Url="/Admin/Announcements" },
                    new DashboardKpi { Title = "Servis",    Value = srvCount.ToString("N0", tr), Icon = "bi-bus-front", Url="/Admin/Transports/Services" }
                }
            };

            return View(vm);
        }

        // Basit JSON endpoint: kullanıcı listesi (id, username, email, roles)
[HttpGet]
public async Task<IActionResult> Users()
{
    // Sadece Employee kaydı olan kullanıcıları listele
    var users = await _userManager.Users
        .Select(u => new { u.Id, u.UserName, u.Email })
        .ToListAsync();

    var result = new List<object>();

    foreach (var u in users)
    {
        // UserName/Email tamamen boşsa tabloya eklemeyelim
        if (string.IsNullOrWhiteSpace(u.UserName) && string.IsNullOrWhiteSpace(u.Email))
            continue;

        // Employee ile ilişkilendirilmiş mi?
        var hasEmployee = await _ctx.Employees
            .AsNoTracking()
            .AnyAsync(e => e.UserId == u.Id);

        if (!hasEmployee)
            continue;

        var user = await _userManager.FindByIdAsync(u.Id);
        var roles = user != null ? await _userManager.GetRolesAsync(user) : Array.Empty<string>();

        result.Add(new { u.Id, u.UserName, u.Email, Roles = roles });
    }

    return Json(result);
}

        // POST: /Admin/Dashboard/UpdateRoles
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(string userId, List<string> roles)
        {
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var current = await _userManager.GetRolesAsync(user);
            var toAdd = roles.Except(current).ToArray();
            var toRemove = current.Except(roles).ToArray();

            if (toAdd.Any()) await _userManager.AddToRolesAsync(user, toAdd);
            if (toRemove.Any()) await _userManager.RemoveFromRolesAsync(user, toRemove);

            return Ok();
        }

        // GET: /Admin/Dashboard/ManageUsers
        [HttpGet]
        public IActionResult ManageUsers()
        {
            return View();
        }

        // GET: /Admin/Dashboard/Roles
        [HttpGet]
        public async Task<IActionResult> Roles()
        {
            var roles = await _roleManager.Roles
                .Select(r => r.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToListAsync();
            return Json(roles);
        }
    }
}
