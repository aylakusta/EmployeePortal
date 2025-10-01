using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DepartmentsController : Controller
    {
        private readonly ApplicationDbContext _context;



        public DepartmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Listeleme
        public async Task<IActionResult> Index()
        {
            var list = await _context.Departments.ToListAsync();
            return View(list);
        }

        // Yeni Ekle (GET)
        public IActionResult Create() => View();

        // Yeni Ekle (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Department department)
        {
            if (!ModelState.IsValid) return View(department);

            _context.Add(department);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Düzenle (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept == null) return NotFound();
            return View(dept);
        }

        // Düzenle (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Department department)
        {
            if (id != department.Id) return BadRequest();
            if (!ModelState.IsValid) return View(department);

            _context.Update(department);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Detay
        public async Task<IActionResult> Details(int id)
        {
            var dept = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dept == null) return NotFound();
            return View(dept);
        }

        // Sil (GET)
        public async Task<IActionResult> Delete(int id)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept == null) return NotFound();
            return View(dept);
        }

        // Sil (POST)
        [HttpPost, ValidateAntiForgeryToken, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept == null)
            {
                TempData["Warn"] = "Departman bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Departments.Remove(dept);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Departman silindi.";
            }
            catch (DbUpdateException)
            {
                // İlişkili kayıtlar (Employees vs.) yüzünden silinemeyebilir:
                TempData["Error"] = "Departman silinemedi. İlişkili kayıtlar olabilir.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
