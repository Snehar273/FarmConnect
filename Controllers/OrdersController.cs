namespace FarmConnect.Controllers;

[Authorize]
public class OrdersController(IOrderService orders, UserManager<ApplicationUser> um) : Controller
{
    public async Task<IActionResult> Index()
    {
        var uid = um.GetUserId(User);
        var list = User.IsInRole("Farmer") ? await orders.GetFarmerOrdersAsync(uid)
                 : User.IsInRole("Buyer") ? await orders.GetBuyerOrdersAsync(uid)      // buyers only see their own orders
                 : await orders.GetAllOrdersAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Buyer")]
    public async Task<IActionResult> Place(int listingId, decimal quantity)
    {
        var r = await orders.PlaceOrderAsync(um.GetUserId(User), listingId, quantity);
        if (!r.Success) { TempData["Error"] = r.Error; return RedirectToAction("Details", "Market", new { id = listingId }); }
        TempData["Ok"] = $"Order {r.Order.OrderNo} placed! Transport is being arranged automatically.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Farmer,Buyer")]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var r = await orders.UpdateStatusAsync(id, um.GetUserId(User), User.IsInRole("Farmer"), status);
        if (r.NotFound) return NotFound();
        TempData[r.Ok ? "Ok" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}