
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FarmConnect.Data;
using FarmConnect.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FarmConnect.Controllers;

[Authorize(Roles = "Farmer")]
public class ListingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _um;
    private readonly IWebHostEnvironment _env;

    private static readonly string[] AllowedExtensions =
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public ListingsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> um,
        IWebHostEnvironment env)
    {
        _db = db;
        _um = um;
        _env = env;
    }

    private string Uid => _um.GetUserId(User) ?? string.Empty;

    // Find a listing belonging to the currently logged-in farmer.
    private Task<Listing> Mine(int id)
    {
        return _db.Listings.FirstOrDefaultAsync(
            l => l.Id == id && l.FarmerId == Uid
        );
    }

    // GET: Listings
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var listings = await _db.Listings
            .Include(l => l.Produce)
            .Where(l => l.FarmerId == Uid)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return View(listings);
    }

    // Load produce dropdown.
    private async Task LoadProduce()
    {
        var produce = await _db.ProduceItems
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.Produce = new SelectList(
            produce,
            "ProduceId",
            "Name"
        );
    }

    // Validate that the uploaded file has a supported image signature.
    private static async Task<bool> LooksLikeImage(IFormFile file)
    {
        if (file == null || file.Length < 12)
        {
            return false;
        }

        var header = new byte[12];

        await using var stream = file.OpenReadStream();

        var bytesRead = await stream.ReadAsync(header.AsMemory());

        if (bytesRead < 12)
        {
            return false;
        }

        // JPEG signature.
        var jpg =
            header[0] == 0xFF &&
            header[1] == 0xD8 &&
            header[2] == 0xFF;

        // PNG signature.
        var png =
            header[0] == 0x89 &&
            header[1] == 0x50 &&
            header[2] == 0x4E &&
            header[3] == 0x47 &&
            header[4] == 0x0D &&
            header[5] == 0x0A &&
            header[6] == 0x1A &&
            header[7] == 0x0A;

        // WEBP signature.
        var webp =
            header[0] == 0x52 &&
            header[1] == 0x49 &&
            header[2] == 0x46 &&
            header[3] == 0x46 &&
            header[8] == 0x57 &&
            header[9] == 0x45 &&
            header[10] == 0x42 &&
            header[11] == 0x50;

        return jpg || png || webp;
    }

    // Validate listing form and uploaded image.
    private async Task Check(ListingForm f)
    {
        if (f.Image != null && f.Image.Length > 0)
        {
            var extension = Path.GetExtension(f.Image.FileName)
                .ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "Image",
                    "Use a JPG, JPEG, PNG or WEBP image."
                );
            }
            else if (f.Image.Length > 2_000_000)
            {
                ModelState.AddModelError(
                    "Image",
                    "Image must be smaller than 2 MB."
                );
            }
            else if (!await LooksLikeImage(f.Image))
            {
                ModelState.AddModelError(
                    "Image",
                    "This file is not a valid image."
                );
            }
        }

        var produceExists = await _db.ProduceItems
            .AnyAsync(p => p.ProduceId == f.ProduceId);

        if (!produceExists)
        {
            ModelState.AddModelError(
                "ProduceId",
                "Select a valid produce."
            );
        }
    }

    // Save uploaded image.
    private async Task<string> Save(IFormFile img)
    {
        if (img == null || img.Length == 0)
        {
            return null;
        }

        var webRoot = _env.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot"
            );
        }

        var directory = Path.Combine(webRoot, "uploads");

        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(img.FileName)
            .ToLowerInvariant();

        var fileName = Guid.NewGuid().ToString("N") + extension;

        var filePath = Path.Combine(directory, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await img.CopyToAsync(stream);
        }

        return "/uploads/" + fileName;
    }

    // Apply form data to listing.
    private static void Apply(Listing l, ListingForm f)
    {
        l.ProduceId = f.ProduceId;
        l.Variety = f.Variety;
        l.Grade = f.Grade;
        l.QuantityAvailable = f.QuantityAvailable;
        l.Unit = f.Unit;
        l.MinOrderQty = f.MinOrderQty;
        l.PricePerUnit = f.PricePerUnit;

        if (f.HarvestDate.HasValue)
        {
            l.HarvestDate = f.HarvestDate.Value.Date;
        }

        l.Description = f.Description;
        l.Location = f.Location;

        // Reactivate a previously sold-out listing when restocked.
        if (l.Status == ListingStatus.SoldOut &&
            l.QuantityAvailable > 0)
        {
            l.Status = ListingStatus.Active;
        }
    }

    // GET: Listings/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadProduce();

        return View("Form", new ListingForm
        {
            HarvestDate = DateTime.Today
        });
    }

    // POST: Listings/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingForm f)
    {
        await Check(f);

        if (!ModelState.IsValid)
        {
            await LoadProduce();
            return View("Form", f);
        }

        var listing = new Listing
        {
            FarmerId = Uid
        };

        Apply(listing, f);

        listing.ImagePath = await Save(f.Image);

        _db.Listings.Add(listing);

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Listing published.";

        return RedirectToAction(nameof(Index));
    }

    // GET: Listings/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var listing = await Mine(id);

        if (listing == null)
        {
            return NotFound();
        }

        await LoadProduce();

        var form = new ListingForm
        {
            Id = listing.Id,
            ProduceId = listing.ProduceId,
            Variety = listing.Variety,
            Grade = listing.Grade,
            QuantityAvailable = listing.QuantityAvailable,
            Unit = listing.Unit,
            MinOrderQty = listing.MinOrderQty,
            PricePerUnit = listing.PricePerUnit,
            HarvestDate = listing.HarvestDate,
            Description = listing.Description,
            Location = listing.Location,
            ExistingImage = listing.ImagePath
        };

        return View("Form", form);
    }

    // POST: Listings/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ListingForm f)
    {
        var listing = await Mine(id);

        if (listing == null)
        {
            return NotFound();
        }

        f.Id = id;
        f.ExistingImage = listing.ImagePath;

        await Check(f);

        if (!ModelState.IsValid)
        {
            await LoadProduce();
            return View("Form", f);
        }

        Apply(listing, f);

        if (f.Image != null && f.Image.Length > 0)
        {
            listing.ImagePath = await Save(f.Image);
        }

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Listing updated.";

        return RedirectToAction(nameof(Index));
    }

    // POST: Listings/Toggle/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var listing = await Mine(id);

        if (listing == null)
        {
            return NotFound();
        }

        if (listing.Status == ListingStatus.Active)
        {
            listing.Status = ListingStatus.Inactive;
        }
        else if (listing.QuantityAvailable > 0)
        {
            listing.Status = ListingStatus.Active;
        }

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST: Listings/Remove/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var listing = await Mine(id);

        if (listing == null)
        {
            return NotFound();
        }

        var hasOrders = await _db.OrderItems
            .AnyAsync(i => i.ListingId == id);

        if (hasOrders)
        {
            // Preserve listing history when orders exist.
            listing.Status = ListingStatus.Inactive;

            TempData["Ok"] =
                "Listing has orders, so it was deactivated. Order history is preserved.";
        }
        else
        {
            _db.Listings.Remove(listing);

            TempData["Ok"] = "Listing removed.";
        }

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
