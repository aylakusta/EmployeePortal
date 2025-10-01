using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebUI.Data;

public class TransportNavViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _ctx;
    public TransportNavViewComponent(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var uid = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uid)) return View("Default", false);

        var emp = await _ctx.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == uid);

        var enabled = emp != null
                      && emp.UsesTransport
                      && !(emp.Category == WebUI.Models.Employee.EmployeeCategory.WhiteCollar && emp.HasCompanyCar);

        return View("Default", enabled);
    }
}
