using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebUI.Models;
using WebUI.Data;

namespace WebUI.Controllers
{
    [Authorize]
    [ServiceFilter(typeof(EnsureTransportEnabledFilter))]
    public class TransportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransportsController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var q = _context.Transports.AsQueryable();
            if (!isAdmin) q = q.Where(t => t.UserId == userId);

            var list = await q.OrderByDescending(t => t.TravelDate).ToListAsync();
            return View(list);
        }

        [HttpGet]
        public IActionResult Create() => View();
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Transport model)
        {
            ModelState.Remove(nameof(Transport.UserId));
            if (!ModelState.IsValid) return View(model);

            model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _context.Transports.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public IActionResult Disabled(string? reason = null)
        {
            ViewData["Title"] = "Servis Kullanimi";
            ViewBag.Reason = reason;
            return View();
        }
    }
}