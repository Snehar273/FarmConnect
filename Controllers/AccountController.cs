using Microsoft.AspNetCore.RateLimiting;

namespace FarmConnect.Controllers;

public class AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, ApplicationDbContext db, ILogger<AccountController> log) : Controller
{
    [HttpGet] public IActionResult Register(string role = "Buyer") => View(new RegisterVm { Role = role == "Farmer" ? "Farmer" : "Buyer" });

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterVm m)
    {
        if (m.Role != "Farmer" && m.Role != "Buyer") ModelState.AddModelError("Role", "Choose Farmer or Buyer.");   // Admin is never selectable
        if (!ModelState.IsValid) return View(m);

        var email = m.Email.Trim();
        var u = new ApplicationUser { UserName = email, Email = email, FullName = m.FullName.Trim(), PhoneNumber = m.Mobile };
        var r = await um.CreateAsync(u, m.Password);
        if (!r.Succeeded)
        {
            // never reveal whether an email address is already registered
            if (r.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                ModelState.AddModelError("", "We could not create this account. If you already registered, please log in.");
            foreach (var e in r.Errors.Where(e => e.Code is not ("DuplicateEmail" or "DuplicateUserName")))
                ModelState.AddModelError("", e.Description);
            return View(m);
        }
        await um.AddToRoleAsync(u, m.Role);
        if (m.Role == "Farmer") db.FarmerProfiles.Add(new FarmerProfile { UserId = u.Id, Location = m.Location });
        else db.BuyerProfiles.Add(new BuyerProfile { UserId = u.Id, Address = m.Location });
        await db.SaveChangesAsync();
        await sm.SignInAsync(u, false);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet] public IActionResult Login(string returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(new LoginVm()); }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginVm m, string returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(m);
        var r = await sm.PasswordSignInAsync(m.Email.Trim(), m.Password, m.RememberMe, lockoutOnFailure: true);
        if (r.Succeeded) return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Dashboard");
        if (r.IsLockedOut) log.LogWarning("Locked-out login attempt from {Ip}", HttpContext.Connection.RemoteIpAddress);
        ModelState.AddModelError("", r.IsLockedOut ? "This account is temporarily locked. Please try again later or contact the admin." : "Invalid email or password.");
        m.Password = null;
        return View(m);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await sm.SignOutAsync(); return RedirectToAction("Index", "Home"); }

    public IActionResult Denied() => View();
}