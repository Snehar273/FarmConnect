namespace FarmConnect.Controllers;

[Authorize(Roles = "Farmer")]
public class ListingsController(ApplicationDbContext db, UserManager<ApplicationUser> um, IWebHostEnvironment env) : Controller
{
    static readonly string[] Ext = { ".jpg", ".jpeg", ".png", ".webp" };
    string Uid => um.GetUserId(User);
    Task<Listing> Mine(int id) => db.Listings.FirstOrDefaultAsync(l => l.Id == id && l.FarmerId == Uid); // ownership enforced here

    public async Task<IActionResult> Index() =>
        View(await db.Listings.Include(l => l.Produce).Where(l => l.FarmerId == Uid).OrderByDescending(l => l.CreatedAt).ToListAsync());

    async Task Lists() => ViewBag.Produce = new SelectList(await db.ProduceItems.OrderBy(p => p.Name).ToListAsync(), "ProduceId", "Name");

    async Task Check(ListingForm f)
    {
        if (f.Image != null && (!Ext.Contains(Path.GetExtension(f.Image.FileName).ToLowerInvariant()) || f.Image.Length > 2_000_000))
            ModelState.AddModelError("Image", "Use a JPG/PNG/WEBP image under 2 MB.");
        if (!await db.ProduceItems.AnyAsync(p => p.ProduceId == f.ProduceId))
            ModelState.AddModelError("ProduceId", "Select a valid produce.");
    }

    async Task<string> Save(IFormFile img)
    {
        if (img == null) return null;
        var dir = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(img.FileName).ToLowerInvariant();
        await using var fs = System.IO.File.Create(Path.Combine(dir, name));
        await img.CopyToAsync(fs);
        return "/uploads/" + name;
    }

    static void Apply(Listing l, ListingForm f)
    {
        l.ProduceId = f.ProduceId; l.Variety = f.Variety; l.Grade = f.Grade; l.QuantityAvailable = f.QuantityAvailable;
        l.Unit = f.Unit; l.MinOrderQty = f.MinOrderQty; l.PricePerUnit = f.PricePerUnit;
        l.HarvestDate = f.HarvestDate.Value.Date; l.Description = f.Description; l.Location = f.Location;
        if (l.Status == ListingStatus.SoldOut) l.Status = ListingStatus.Active; // restocked
    }

    public async Task<IActionResult> Create() { await Lists(); return View("Form", new ListingForm { HarvestDate = DateTime.Today }); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingForm f)
    {
        await Check(f);
        if (!ModelState.IsValid) { await Lists(); return View("Form", f); }
        var l = new Listing { FarmerId = Uid };
        Apply(l, f);
        l.ImagePath = await Save(f.Image);
        db.Listings.Add(l);
        await db.SaveChangesAsync();
        TempData["Ok"] = "Listing published.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var l = await Mine(id);
        if (l == null) return NotFound();
        await Lists();
        return View("Form", new ListingForm
        {
            Id = l.Id, ProduceId = l.ProduceId, Variety = l.Variety, Grade = l.Grade, QuantityAvailable = l.QuantityAvailable, Unit = l.Unit,
            MinOrderQty = l.MinOrderQty, PricePerUnit = l.PricePerUnit, HarvestDate = l.HarvestDate, Description = l.Description, Location = l.Location, ExistingImage = l.ImagePath
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ListingForm f)
    {
        var l = await Mine(id);
        if (l == null) return NotFound();
        f.Id = id; f.ExistingImage = l.ImagePath;
        await Check(f);
        if (!ModelState.IsValid) { await Lists(); return View("Form", f); }
        Apply(l, f);
        if (f.Image != null) l.ImagePath = await Save(f.Image);
        await db.SaveChangesAsync();
        TempData["Ok"] = "Listing updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var l = await Mine(id);
        if (l == null) return NotFound();
        if (l.Status == ListingStatus.Active) l.Status = ListingStatus.Inactive;
        else if (l.QuantityAvailable > 0) l.Status = ListingStatus.Active;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var l = await Mine(id);
        if (l == null) return NotFound();
        if (await db.OrderItems.AnyAsync(i => i.ListingId == id)) { l.Status = ListingStatus.Inactive; TempData["Ok"] = "Listing has orders, so it was deactivated (history preserved)."; }
        else { db.Listings.Remove(l); TempData["Ok"] = "Listing removed."; }
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
