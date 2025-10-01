using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Filters
{
    public class EnsureBlueCollarFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _ctx;

        public EnsureBlueCollarFilter(ApplicationDbContext ctx) => _ctx = ctx;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;

            // Oturum yoksa login'e yönlendir (Challenge)
            if (!user?.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            // Adminler istersen geçebilsin (istersen bu bloğu kaldır)
            if (user.IsInRole("Admin"))
            {
                await next();
                return;
            }

            var uid = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Employee? emp = null;

            if (!string.IsNullOrEmpty(uid))
            {
                emp = await _ctx.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == uid);
            }

            // Fallback: Email claim ile eşle
            if (emp == null)
            {
                var email = user.FindFirst(ClaimTypes.Email)?.Value;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    emp = await _ctx.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Email == email);
                }
            }

            // Beyaz yaka ise AccessDenied
            if (emp == null || emp.Category == Employee.EmployeeCategory.WhiteCollar)
            {
                // Cookie auth’un AccessDeniedPath’ına gitsin
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                // Alternatif: context.Result = new ForbidResult();
                return;
            }

            await next();
        }
    }
}
