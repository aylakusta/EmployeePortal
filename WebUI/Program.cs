using WebUI.Services;
﻿using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using WebUI.Data;
using WebUI.Hubs;
using WebUI.Models;
using WebUI.Services.Auditing;
using WebUI.Services.Notifications;
using WebUI.Services.Settings;
using WebUI.Filters;
using System.Text;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);


var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications:Smtp"));
builder.Services.AddSingleton<INotificationService, NotificationService>();

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IAuditLogger, FileAuditLogger>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddScoped<EnsureTransportEnabledFilter>();
builder.Services.AddScoped<EnsureBlueCollarFilter>();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("Uploads", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("Forms", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddScoped<WebUI.Infrastructure.Repositories.IRepository, WebUI.Infrastructure.Repositories.EfRepository>();


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageRoles", policy =>
        policy.RequireRole("Admin").RequireAuthenticatedUser());
});


var app = builder.Build();

// Seed Turkish global job titles
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<WebUI.Data.ApplicationDbContext>();
    if (!ctx.JobTitles.Any())
    {
        ctx.JobTitles.AddRange(new[] {
            new WebUI.Models.JobTitle { Name = "Genel Müdür" },
            new WebUI.Models.JobTitle { Name = "Müdür" },
            new WebUI.Models.JobTitle { Name = "Direktör" },
            new WebUI.Models.JobTitle { Name = "Takım Lideri" },
            new WebUI.Models.JobTitle { Name = "Yazılım Geliştirici" },
            new WebUI.Models.JobTitle { Name = "Kıdemli Yazılım Geliştirici" },
            new WebUI.Models.JobTitle { Name = "İK Uzmanı" },
            new WebUI.Models.JobTitle { Name = "Muhasebe Uzmanı" },
            new WebUI.Models.JobTitle { Name = "Satış Uzmanı" },
            new WebUI.Models.JobTitle { Name = "Operasyon Uzmanı" },
            new WebUI.Models.JobTitle { Name = "Stajyer" }
        });
        ctx.SaveChanges();
    }
}


using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var ctx = sp.GetRequiredService<ApplicationDbContext>();
    await ctx.Database.MigrateAsync();

    var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = new[] { "Admin", "User", "SuperUser" };
    foreach (var r in roles)
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));

    var admin = await userMgr.FindByEmailAsync("admin@portal.com");
    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = "admin",
            Email = "admin@portal.com",
        };
        await userMgr.CreateAsync(admin, "Admin123!");
        await userMgr.AddToRoleAsync(admin, "Admin");
    }
    else if (!await userMgr.IsInRoleAsync(admin, "Admin"))
    {
        await userMgr.AddToRoleAsync(admin, "Admin");
    }

    var normal = await userMgr.FindByEmailAsync("user@portal.com");
    if (normal == null)
    {
        normal = new ApplicationUser
        {
            UserName = "user",
            Email = "user@portal.com",
        };
        await userMgr.CreateAsync(normal, "User123!");
    }

    if (await userMgr.IsInRoleAsync(normal, "Admin"))
        await userMgr.RemoveFromRoleAsync(normal, "Admin");

    if (!await userMgr.IsInRoleAsync(normal, "User"))
        await userMgr.AddToRoleAsync(normal, "User");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.MapHub<AdminHub>("/hubs/admin");

app.MapGet("/_routes", (EndpointDataSource eds) =>
{
    var list = eds.Endpoints
        .OfType<RouteEndpoint>()
        .Select(e => new
        {
            pattern = e.RoutePattern.RawText,
            order = e.Order,
            displayName = e.DisplayName
        })
        .OrderBy(e => e.order)
        .ThenBy(e => e.pattern);
    return Results.Json(list);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
