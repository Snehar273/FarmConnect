using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => { o.AddServerHeader = false; o.Limits.MaxRequestBodySize = 6_000_000; });

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 8;
    o.Password.RequiredUniqueChars = 4;
    o.Lockout.AllowedForNewUsers = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Denied";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // Secure automatically when served over HTTPS
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});
// a locked / changed account is signed out within ~1 minute
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddAntiforgery(o => { o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllersWithViews();
builder.Services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = builder.Configuration["Redis:Configuration"] ?? "localhost:6379,abortConnect=false,connectTimeout=1500,syncTimeout=1500";
    o.InstanceName = "farmconnect:";
});

builder.Services.AddScoped<IFarmerService, FarmerService>();
builder.Services.AddScoped<IProduceService, ProduceService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ILogisticsService, LogisticsService>();
builder.Services.AddScoped<IPriceCacheService, PriceCacheService>();
builder.Services.AddScoped<IEventHandler, OrderPlacedHandler>();
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await DbSeeder.InitAsync(scope.ServiceProvider, app.Configuration);

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();

const string csp = "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
    "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
    "img-src 'self' data: https://images.unsplash.com; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    // enforced in production; report-only in Development (violations show in the browser console)
    h[app.Environment.IsDevelopment() ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy"] = csp;
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (ctx, next) =>   // private pages are never stored by the browser or proxies
{
    if (ctx.User.Identity?.IsAuthenticated == true) ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();