using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Models;
using WebUI.Data;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public DocumentsController(ApplicationDbContext ctx, IWebHostEnvironment env) { _context = ctx; _env = env; }

        public async Task<IActionResult> Index()
        {
            var list = await _context.Documents.AsNoTracking().OrderByDescending(d => d.UploadedAt).ToListAsync();

            return View(list);
        }

        [HttpGet] public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Document model, IFormFile? file)
        {
            if (file == null || file.Length == 0) ModelState.AddModelError("", "Dosya seçin.");
            if (!ModelState.IsValid) return View(model);

            var dir = Path.Combine(_env.WebRootPath, "uploads", "documents");
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid()}_{file!.FileName}";
            using (var fs = new FileStream(Path.Combine(dir, name), FileMode.Create))
                await file.CopyToAsync(fs);

            model.FileName = file.FileName;
            model.FilePath = $"/uploads/documents/{name}";
            model.UploadDate = DateTime.UtcNow;

            _context.Documents.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Yeni: talep görüntüleme için çakışmayı önleyen isim
        // GET: /Admin/Documents/GetRequest/5
        [HttpGet]
        public async Task<IActionResult> GetRequest(int id)
        {
            var req = await _context.DocumentRequests
                .Include(r => r.AttendanceRef)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null) return NotFound();

            var viewPath = req.Type switch
            {
                DocumentRequestType.MedicalReport => "~/Views/Documents/RequestReport.cshtml",
                DocumentRequestType.AnnualLeave => "~/Views/Documents/RequestLeave.cshtml",
                _ => "~/Views/Documents/RequestLeave.cshtml"
            };

            return View(viewPath, req);
        }
    }
}
