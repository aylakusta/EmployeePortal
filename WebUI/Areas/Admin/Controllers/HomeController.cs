using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            // /Admin/Home -> /Admin/Dashboard
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
    }
}
