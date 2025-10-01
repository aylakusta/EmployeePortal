using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Filters;
using WebUI.Models;

namespace WebUI.Controllers
{
    [Authorize]
    [ServiceFilter(typeof(EnsureBlueCollarFilter))]
    public class ShiftsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShiftsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Shifts
        public async Task<IActionResult> Index()
        {
            // Null referans uyarısını önlemek için Include
            var list = await _context.Shifts
                .Include(s => s.User)
                .OrderByDescending(s => s.Date)
                .ToListAsync();

            return View(list);
        }

        // GET: /Shifts/Create
        public IActionResult Create()
        {
            return View(new Shift { Date = DateTime.UtcNow.Date });
        }

        // POST: /Shifts/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Shift model)
        {
            if (!ModelState.IsValid) return View(model);

            // Kullanıcının kendi vardiyasını oluşturduğunu varsayıyoruz
            model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _context.Shifts.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Shifts/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound();
            return View(shift);
        }

        // POST: /Shifts/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Shift model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid) return View(model);

            _context.Update(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Shifts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var shift = await _context.Shifts
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (shift == null) return NotFound();
            return View(shift);
        }

        // GET: /Shifts/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound();
            return View(shift);
        }

        // POST: /Shifts/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift != null) _context.Shifts.Remove(shift);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
