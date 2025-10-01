using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebUI.Services;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "CanManageRoles")]
    [Route("Admin/Users")] // <-- net prefix
    public class UsersController : Controller
    {
        private readonly IUserRoleService _svc;
        private readonly IHttpContextAccessor _http;

        public UsersController(IUserRoleService svc, IHttpContextAccessor http)
        {
            _svc = svc;
            _http = http;
        }

        // GET /Admin/Users/List
        [HttpGet("List")]
        public async Task<IActionResult> List(CancellationToken ct)
            => Json(await _svc.ListEmployeeUsersAsync(ct));

        // GET /Admin/Users/Roles
        [HttpGet("Roles")]
        public async Task<IActionResult> Roles(CancellationToken ct)
            => Json(await _svc.GetAllRolesAsync(ct));

        // POST /Admin/Users/UpdateRoles
        [HttpPost("UpdateRoles")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(string userId, List<string> roles, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest();
            var by = _http.HttpContext?.User?.Identity?.Name ?? "unknown";
            await _svc.UpdateRolesAsync(userId, roles ?? new List<string>(), by, ct);
            return Ok();
        }
    }
}
