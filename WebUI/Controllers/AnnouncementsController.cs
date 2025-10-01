using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Models;
using WebUI.Data;

namespace WebUI.Controllers
{
    [Authorize]
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementsController(ApplicationDbContext context) => _context = context;

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Announcements
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(list);
        }


        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create() => View();

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Announcement model)
        {
            if (!ModelState.IsValid) return View(model);
            model.CreatedAt = DateTime.UtcNow;
            _context.Announcements.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
