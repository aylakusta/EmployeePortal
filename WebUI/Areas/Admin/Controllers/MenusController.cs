using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Models;
using WebUI.Data;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MenusController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        public MenusController(ApplicationDbContext ctx) => _ctx = ctx;

        public async Task<IActionResult> Index()
        {
            var list = await _ctx.Menus.OrderBy(m => m.CreatedAt).ToListAsync();
            return View(list);
        }

        [HttpGet] public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Menu model)
        {
            if (!ModelState.IsValid) return View(model);
            _ctx.Menus.Add(model);
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
