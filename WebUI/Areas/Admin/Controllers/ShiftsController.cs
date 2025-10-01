using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ShiftsController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShiftsController(ApplicationDbContext ctx, UserManager<ApplicationUser> userManager)
        {
            _ctx = ctx;
            _userManager = userManager;
        }

        // GET: /Admin/Shifts
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var list = await _ctx.Shifts
                                 .Include(s => s.User)
                                 .AsNoTracking()
                                 .OrderByDescending(s => s.Date)
                                 .ToListAsync();
            return View(list);
        }

        // GET: /Admin/Shifts/Create
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Users = _userManager.Users.ToList();
            return View();
        }

        // POST: /Admin/Shifts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Shift model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Users = _userManager.Users.ToList();
                return View(model);
            }

            _ctx.Shifts.Add(model);
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Shifts/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var shift = await _ctx.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            ViewBag.Users = _userManager.Users.ToList();
            return View(shift);
        }

        // POST: /Admin/Shifts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Shift model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Users = _userManager.Users.ToList();
                return View(model);
            }

            _ctx.Entry(model).State = EntityState.Modified;
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Shifts/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var shift = await _ctx.Shifts
                                  .Include(s => s.User)
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(s => s.Id == id);
            if (shift == null) return NotFound();
            return View(shift);
        }

        // POST: /Admin/Shifts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shift = await _ctx.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            _ctx.Shifts.Remove(shift);
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
