namespace FarmConnect.Controllers;

[Authorize(Roles = "Farmer")]
public class EarningsController(ApplicationDbContext db, UserManager<ApplicationUser> um) : Controller
{
    public async Task<IActionResult> Index(string period = "monthly", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var today = DateTime.Today; DateTime s, e;
        switch (period)
        {
            case "daily": s = e = today; break;
            case "weekly": s = today.AddDays(-6); e = today; break;
            case "custom": s = (fromDate ?? today.AddDays(-30)).Date; e = (toDate ?? today).Date; break;
            default: period = "monthly"; s = new DateTime(today.Year, today.Month, 1); e = today; break;
        }
        var end = e.AddDays(1); var uid = um.GetUserId(User);
        // Only COMPLETED orders count as earnings
        var orders = await db.Orders.Include(o => o.Items).Include(o => o.Buyer)
            .Where(o => o.FarmerId == uid && o.Status == OrderStatus.Completed && o.CompletedAt >= s && o.CompletedAt < end)
            .OrderByDescending(o => o.CompletedAt).ToListAsync();
        var exp = await db.SellingExpenses.Where(x => x.FarmerId == uid && x.ExpenseDate >= s && x.ExpenseDate < end).OrderByDescending(x => x.ExpenseDate).ToListAsync();
        var vm = new EarningsVm
        {
            Period = period, From = s, To = e, Orders = orders, Expenses = exp,
            Gross = orders.Sum(o => o.Items.Sum(i => i.Quantity * i.AgreedUnitPrice)),
            Qty = orders.Sum(o => o.Items.Sum(i => i.Quantity)),
            Costs = exp.Sum(x => x.Amount)
        };
        vm.Net = vm.Gross - vm.Costs;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpense(int orderId, string description, decimal amount)
    {
        var uid = um.GetUserId(User);
        var ok = await db.Orders.AnyAsync(o => o.Id == orderId && o.FarmerId == uid && o.Status == OrderStatus.Completed);
        if (!ok || amount <= 0 || string.IsNullOrWhiteSpace(description) || description.Length > 200) TempData["Error"] = "Invalid expense entry.";
        else
        {
            db.SellingExpenses.Add(new SellingExpense { OrderId = orderId, FarmerId = uid, Description = description.Trim(), Amount = amount });
            await db.SaveChangesAsync();
            TempData["Ok"] = "Selling cost recorded.";
        }
        return RedirectToAction(nameof(Index));
    }
}
