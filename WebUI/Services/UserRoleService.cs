using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebUI.Data;
using WebUI.Models; // <-- DOĞRU namespace (Auth değil)

namespace WebUI.Services
{
    public interface IUserRoleService
    {
        Task<IReadOnlyList<string>> GetAllRolesAsync(CancellationToken ct = default);
        Task<List<UserWithRolesDto>> ListEmployeeUsersAsync(CancellationToken ct = default);
        Task UpdateRolesAsync(string userId, IEnumerable<string> roles, string performedByUserName, CancellationToken ct = default);
    }

    public class UserWithRolesDto
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string? Email { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class UserRoleService : IUserRoleService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _ctx;
        private readonly ILogger<UserRoleService> _logger;

        public UserRoleService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext ctx,
            ILogger<UserRoleService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _ctx = ctx;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> GetAllRolesAsync(CancellationToken ct = default)
        {
            var roles = await _roleManager.Roles
                .Select(r => r.Name!)
                .OrderBy(x => x)
                .ToListAsync(ct);
            return roles;
        }

        public async Task<List<UserWithRolesDto>> ListEmployeeUsersAsync(CancellationToken ct = default)
        {
            // Sadece Employees tablosunda karşılığı olan kullanıcıları döndür
            var employeeUserIds = await _ctx.Employees.AsNoTracking()
                .Where(e => e.UserId != null)
                .Select(e => e.UserId!)
                .Distinct()
                .ToListAsync(ct);

            var users = _userManager.Users
                .Where(u => employeeUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.Email });

            var list = await users.ToListAsync(ct);
            var result = new List<UserWithRolesDto>(list.Count);

            foreach (var u in list)
            {
                if (string.IsNullOrWhiteSpace(u.UserName) && string.IsNullOrWhiteSpace(u.Email)) continue;

                var user = await _userManager.FindByIdAsync(u.Id);
                var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();

                result.Add(new UserWithRolesDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    Email = u.Email,
                    Roles = roles.OrderBy(r => r).ToList()
                });
            }

            return result;
        }

        public async Task UpdateRolesAsync(string userId, IEnumerable<string> roles, string performedByUserName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId required", nameof(userId));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new InvalidOperationException("User not found");

            // Sadece geçerli roller
            var validRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync(ct);
            var desired = roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(r => validRoles.Contains(r))
                .ToArray();

            var current = await _userManager.GetRolesAsync(user);
            var toAdd = desired.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
            var toRemove = current.Except(desired, StringComparer.OrdinalIgnoreCase).ToArray();

            if (toAdd.Any()) await _userManager.AddToRolesAsync(user, toAdd);
            if (toRemove.Any()) await _userManager.RemoveFromRolesAsync(user, toRemove);

            _logger.LogInformation("Roles updated for {UserId} by {PerformedBy}: +[{Add}] -[{Remove}]",
                userId, performedByUserName, string.Join(",", toAdd), string.Join(",", toRemove));
        }
    }
}