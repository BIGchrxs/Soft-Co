using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoftCo.Data;
using SoftCo.Models;
using SoftCo.Services;

var builder = WebApplication.CreateBuilder(args);

// Every money field in this application is a decimal bound from a form post. Pinning the request
// culture to InvariantCulture keeps "1234.56" parsing the same way regardless of the server's or
// the browser's locale - on a South African machine the default culture uses a comma as the
// decimal separator, which silently turns 1234.56 into 123456 on the way in.
var invariant = new[] { new CultureInfo("en-ZA") { NumberFormat = CultureInfo.InvariantCulture.NumberFormat } };
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(invariant[0]);
    o.SupportedCultures = invariant;
    o.SupportedUICultures = invariant;
});
CultureInfo.DefaultThreadCurrentCulture = invariant[0];
CultureInfo.DefaultThreadCurrentUICulture = invariant[0];

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(DbConnectionStringFactory.Build(builder.Configuration, builder.Environment)));

// AddIdentity rather than AddDefaultIdentity: the brief requires a branded login portal, so the
// login, logout and access-denied screens are this application's own views rather than the
// scaffolded Identity UI Razor Pages.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    // Brute-force defence: five attempts, then a fifteen minute lockout.
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization();
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Apply migrations and seed roles/bootstrap admin at startup. Behind ECS this runs once per task;
// EF's migration history table makes concurrent attempts safe - the loser sees the migration
// already applied rather than applying it twice.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(scope.ServiceProvider, app.Configuration, app.Environment);
    await TrackerSeed.SeedAsync(db, app.Environment);
}

app.Run();
/* Developer: Christopher Graham
   Code src: please note:
        This code has been created similtaniously with my IDA program,
        it has been created for a subcompany of inhouse design so,
        a lot of the code features have been taken from IDA for more
        effective development*/