namespace FarmConnect.Controllers;

[Authorize(Roles = "Farmer")]
public class EarningsController(IFarmerService farmers, UserManager<ApplicationUser> um) : Controller
{
    public async Task<IActionResult> Index(string period = "monthly", DateTime? fromDate = null, DateTime? toDate = null) =>
        View(await farmers.GetFarmerEarningsAsync(um.GetUserId(User), period, fromDate, toDate));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpense(int orderId, string description, decimal amount)
    {
        var r = await farmers.AddSellingExpenseAsync(um.GetUserId(User), orderId, description, amount);
        TempData[r.Ok ? "Ok" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}