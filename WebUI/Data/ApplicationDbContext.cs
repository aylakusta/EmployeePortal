using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebUI.Models;

namespace WebUI.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<JobTitle> JobTitles { get; set; }   // ✔️ Burada olacak
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<Menu> Menus { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Transport> Transports { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ServiceAssignment> ServiceAssignments { get; set; }
        public DbSet<Attendance> Attendances { get; set; } = default!;
        public DbSet<DocumentRequest> DocumentRequests { get; set; } = default!;


        public DbSet<Attendance> GetAttendances() => Attendances;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);


            builder.Entity<JobTitle>().HasData(
                new JobTitle { Id = 1, Name = "Genel Müdür"},
                new JobTitle { Id = 2, Name = "Direktör"},
                new JobTitle { Id = 3, Name = "Müdür"},
                new JobTitle { Id = 4, Name = "Yönetici"},
                new JobTitle { Id = 5, Name = "Kıdemli Uzman"},
                new JobTitle { Id = 6, Name = "Uzman"},
                new JobTitle { Id = 7, Name = "Asistan"}
            );

            // --- Employee mappings ---
            builder.Entity<Employee>().HasIndex(e => e.UserId);
            builder.Entity<Employee>().Property(e => e.Category).HasConversion<int>();

            builder.Entity<Employee>(e =>
            {
                e.Property(p => p.CreatedByUserId).HasMaxLength(450);
                e.Property(p => p.DepartmentId).IsRequired(false);

                e.HasOne(p => p.DepartmentRef)
                 .WithMany(d => d.Employees)
                 .HasForeignKey(p => p.DepartmentId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(p => p.JobTitleRef)
                 .WithMany()
                 .HasForeignKey(p => p.JobTitleId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Employee>()
                .HasOne(e => e.SupervisorRef)
                .WithMany(s => s.DirectReports)
                .HasForeignKey(e => e.SupervisorId)
                .OnDelete(DeleteBehavior.SetNull);

            // --- Attendance & DocumentRequest ilişkileri (sende zaten vardı) ---
            builder.Entity<Attendance>()
                .HasOne(a => a.DocumentRequestRef)
                .WithOne(r => r.AttendanceRef)
                .HasForeignKey<DocumentRequest>(r => r.AttendanceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Attendance>()
                .Ignore(a => a.DocumentRequestId);

            // Document, Service, ServiceAssignment konfiglerin aynen sende olduğu gibi kalsın
        }
    }
}
