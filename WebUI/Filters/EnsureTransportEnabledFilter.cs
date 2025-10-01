using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using WebUI.Data;

public class EnsureTransportEnabledFilter : IAsyncActionFilter
{
    private readonly ApplicationDbContext _ctx;
    public EnsureTransportEnabledFilter(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor cad &&
            string.Equals(cad.ActionName, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (user?.Identity == null || !user.Identity.IsAuthenticated)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (user.IsInRole("Admin"))
        {
            await next();
            return;
        }

        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uid))
        {
            context.Result = new ChallengeResult();
            return;
        }

        var emp = await _ctx.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == uid);

        var enabled = emp != null
                      && emp.UsesTransport
                      && !(emp.Category == WebUI.Models.Employee.EmployeeCategory.WhiteCollar && emp.HasCompanyCar);

        if (!enabled)
        {
            var reason = emp == null ? "missing" : "disabled";
            context.Result = new RedirectToActionResult("Disabled", "Transports", new { reason });
            return;
        }

        await next();
    }
}