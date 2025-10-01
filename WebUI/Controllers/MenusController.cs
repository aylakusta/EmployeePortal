using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Models;
using WebUI.Data;

namespace WebUI.Controllers
{
    [Authorize]
    public class MenusController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenusController(ApplicationDbContext context) => _context = context;

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Menus
                .OrderBy(m => m.Date)
                .ToListAsync();
            return View(list);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create() => View();

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Menu model)
        {
            if (!ModelState.IsValid) return View(model);
            _context.Menus.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
