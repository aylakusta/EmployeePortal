using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebUI.Data;
using WebUI.Models;

namespace WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class TransportsController : Controller
    {
        private readonly ApplicationDbContext _ctx;

        public TransportsController(ApplicationDbContext ctx)
        {
            _ctx = ctx;
        }

        // === 1) Kim hangi serviste? ===
        // /Admin/Transports
        public async Task<IActionResult> Index()
        {
            var assignments = await _ctx.ServiceAssignments
                .Where(a => a.IsActive)
                .Include(a => a.Employee)
                .Include(a => a.Service)
                .AsNoTracking()
                .OrderBy(a => a.Service!.Name)
                .ThenBy(a => a.Employee!.FirstName)
                .ToListAsync();

            return View(assignments);
        }

        // === 2) Servis listesi ===
        // /Admin/Transports/Services
        public async Task<IActionResult> Services()
        {
            var services = await _ctx.Services
                .Include(s => s.Assignments)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            return View(services);
        }

        // GET: /Admin/Transports/Create
        public IActionResult Create()
        {
            return View(new CreateServiceVm());
        }

        // POST: /Admin/Transports/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateServiceVm vm)
        {
            // ViewModel doğrulaması
            if (!ModelState.IsValid)
                return View(vm);

            // StartTime string -> TimeSpan?
            TimeSpan? startTs = null;
            if (!string.IsNullOrWhiteSpace(vm.StartTime))
            {
                var patterns = new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" };
                TimeSpan parsed = default;
                bool ok = false;
                foreach (var fmt in patterns)
                {
                    if (TimeSpan.TryParseExact(vm.StartTime, fmt, CultureInfo.InvariantCulture, out parsed))
                    { ok = true; break; }
                }
                if (!ok && TimeSpan.TryParse(vm.StartTime, out parsed)) ok = true;

                if (ok)
                {
                    startTs = parsed;
                }
                else
                {
                    ModelState.AddModelError(nameof(vm.StartTime), "Geçerli bir saat seçiniz (örn. 08:30).");
                    return View(vm);
                }
            }
            else
            {
                ModelState.AddModelError(nameof(vm.StartTime), "Geçerli bir saat seçiniz (örn. 08:30).");
                return View(vm);
            }

            var entity = new Service
            {
                Name = vm.Name,
                StartPoint = vm.StartPoint,
                EndPoint = vm.EndPoint,
                StartTime = startTs,
                PlateNumber = vm.PlateNumber,
                SeatCount = vm.SeatCount,
                FuelType = vm.FuelType,
                Brand = vm.Brand,
                Model = vm.Model,
                IsActive = vm.IsActive,
                // CreatedAt: DbContext tarafında default ile doluyor
            };

            _ctx.Services.Add(entity);
            await _ctx.SaveChangesAsync();

            return RedirectToAction(nameof(Services));
        }

        // GET: /Admin/Transports/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _ctx.Services.FindAsync(id);
            if (s == null) return NotFound();
            return View(s);
        }

        // POST: /Admin/Transports/Update/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id)
        {
            var entity = await _ctx.Services.FindAsync(id);
            if (entity == null) return NotFound();

            // Formda olmayan/sistem alanlarını validasyondan çıkar
            ModelState.Remove(nameof(Service.Id));
            ModelState.Remove(nameof(Service.CreatedAt));
            ModelState.Remove(nameof(Service.Assignments));
            ModelState.Remove(nameof(Service.StartTime)); // kendimiz parse edeceğiz

            // StartTime = "HH:mm" değerini güvenle parse et
            var startTimeRaw = Request.Form["StartTime"];
            if (!string.IsNullOrWhiteSpace(startTimeRaw))
            {
                if (TimeSpan.TryParse(startTimeRaw, out var ts))
                    entity.StartTime = ts;
                else
                    ModelState.AddModelError(nameof(Service.StartTime), "Geçerli bir saat giriniz (örn. 08:30).");
            }
            else
            {
                entity.StartTime = null; // boş bırakılmasına izin ver
            }

            // Yalnızca düzenlenebilir alanları güncelle
            var ok = await TryUpdateModelAsync(entity, prefix: "",
                s => s.Name,
                s => s.PlateNumber,
                s => s.StartPoint,
                s => s.EndPoint,
                s => s.SeatCount,
                s => s.FuelType,
                s => s.Brand,
                s => s.Model,
                s => s.IsActive
            );

            if (!ok || !ModelState.IsValid)
                return View("Edit", entity);

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Services));
        }

        // === SERVİS SİL ===
        // GET: /Admin/Transports/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _ctx.Services.FindAsync(id);
            if (s == null) return NotFound();
            return View(s);
        }

        // POST: /Admin/Transports/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var s = await _ctx.Services.FindAsync(id);
            if (s == null) return NotFound();

            _ctx.Services.Remove(s); // Assignments cascade ile silinecekse DbContext’te ayarlı olmalı
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Services));
        }

        // === 4) Atama ===
        // GET: /Admin/Transports/Assign?serviceId=5
        public async Task<IActionResult> Assign(int? serviceId)
        {
            ViewBag.Services = new SelectList(
                await _ctx.Services
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToListAsync(),
                "Id", "Name", serviceId
            );

            var vm = new AssignViewModel { ServiceId = serviceId };

            if (serviceId.HasValue)
            {
                // Halihazırda aktif atanmış çalışanları dışarıda bırak
                var alreadyAssignedEmployeeIds = await _ctx.ServiceAssignments
                    .Where(a => a.IsActive && a.ServiceId == serviceId.Value)
                    .Select(a => a.EmployeeId)
                    .ToListAsync();

                vm.Candidates = await _ctx.Employees
                    .Where(e => !alreadyAssignedEmployeeIds.Contains(e.Id))
                    .OrderBy(e => e.FirstName)
                    .ThenBy(e => e.LastName)
                    .Select(e => new AssignCandidate
                    {
                        EmployeeId = e.Id,
                        FullName = e.FirstName + " " + e.LastName,
                        Department = e.Department
                    })
                    .ToListAsync();
            }

            return View(vm);
        }

        // POST: /Admin/Transports/Assign
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Assign")]
        public async Task<IActionResult> AssignPost(AssignViewModel vm)
        {
            if (!vm.ServiceId.HasValue)
            {
                ModelState.AddModelError("", "Lütfen bir servis seçiniz.");
                return await Assign((int?)null);
            }
            if (vm.SelectedEmployeeIds == null || vm.SelectedEmployeeIds.Count == 0)
            {
                ModelState.AddModelError("", "Lütfen en az bir personel seçiniz.");
                return await Assign(vm.ServiceId);
            }

            // 1) Servis ve mevcut doluluk
            var svc = await _ctx.Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == vm.ServiceId.Value);
            if (svc == null)
            {
                ModelState.AddModelError("", "Seçilen servis bulunamadı.");
                return await Assign((int?)null);
            }

            var activeOnThisService = await _ctx.ServiceAssignments
                .Where(a => a.IsActive && a.ServiceId == vm.ServiceId.Value)
                .Select(a => a.EmployeeId)
                .ToListAsync();

            // Zaten bu serviste aktif olanları çıkar
            var toAssign = vm.SelectedEmployeeIds.Except(activeOnThisService).ToList();

            // 2) Kişi başka bir aktif serviste mi? Onları çıkar (tek aktif servis kuralı)
            var busyElsewhere = await _ctx.ServiceAssignments
                .Where(a => a.IsActive && toAssign.Contains(a.EmployeeId))
                .Select(a => a.EmployeeId)
                .Distinct()
                .ToListAsync();

            if (busyElsewhere.Any())
            {
                toAssign = toAssign.Except(busyElsewhere).ToList();
                TempData["Info"] = $"{busyElsewhere.Count} personel başka bir aktif servisteydi, atlanarak devam edildi.";
            }

            // 3) Kapasite kontrolü
            var currentCount = activeOnThisService.Count;
            var capacityLeft = Math.Max(0, svc.SeatCount - currentCount);

            if (toAssign.Count > capacityLeft)
            {
                TempData["Error"] = $"Bu serviste {currentCount}/{svc.SeatCount} doluluk var. " +
                                    $"Kalan kapasite: {capacityLeft}. Seçtiğiniz {toAssign.Count} kişiden " +
                                    $"{toAssign.Count - capacityLeft} kişi kapasite nedeniyle atanamadı.";
                toAssign = toAssign.Take(capacityLeft).ToList();
            }

            // 4) Kayıtları ekle
            foreach (var empId in toAssign)
            {
                _ctx.ServiceAssignments.Add(new ServiceAssignment
                {
                    ServiceId = vm.ServiceId.Value,
                    EmployeeId = empId
                });
            }

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // === 5) Atama kaldır (tekil) ===
        // POST: /Admin/Transports/Unassign/12
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unassign(int id)
        {
            var a = await _ctx.ServiceAssignments.FindAsync(id);
            if (a == null) return NotFound();

            a.IsActive = false;
            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }

    // === ViewModel'ler (bu dosyada bırakıldı) ===
    public class AssignViewModel
    {
        public int? ServiceId { get; set; }
        public List<AssignCandidate> Candidates { get; set; } = new();
        public List<int>? SelectedEmployeeIds { get; set; }
    }

    public class AssignCandidate
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = "";
        public string? Department { get; set; }
    }
}
