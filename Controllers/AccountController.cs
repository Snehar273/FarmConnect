namespace FarmConnect.Controllers;

public class AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, ApplicationDbContext db) : Controller
{
    [HttpGet] public IActionResult Register(string role = "Buyer") => View(new RegisterVm { Role = role == "Farmer" ? "Farmer" : "Buyer" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm m)
    {
        if (m.Role != "Farmer" && m.Role != "Buyer") ModelState.AddModelError("Role", "Choose Farmer or Buyer."); // Admin is never selectable
        if (!ModelState.IsValid) return View(m);
        var u = new ApplicationUser { UserName = m.Email, Email = m.Email, FullName = m.FullName.Trim(), PhoneNumber = m.Mobile };
        var r = await um.CreateAsync(u, m.Password);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }
        await um.AddToRoleAsync(u, m.Role);
        if (m.Role == "Farmer") db.FarmerProfiles.Add(new FarmerProfile { UserId = u.Id, Location = m.Location });
        else db.BuyerProfiles.Add(new BuyerProfile { UserId = u.Id });
        await db.SaveChangesAsync();
        await sm.SignInAsync(u, false);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet] public IActionResult Login(string returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(new LoginVm()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm m, string returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(m);
        var r = await sm.PasswordSignInAsync(m.Email, m.Password, m.RememberMe, lockoutOnFailure: true);
        if (r.Succeeded) return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Dashboard");
        ModelState.AddModelError("", r.IsLockedOut ? "This account is locked. Please try later or contact the admin." : "Invalid email or password.");
        return View(m);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await sm.SignOutAsync(); return RedirectToAction("Index", "Home"); }

    public IActionResult Denied() => View();
}
