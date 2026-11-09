
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FarmConnect.Models;

public class RegisterVm
{
    [Required, StringLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required,
     RegularExpression(@"^[6-9]\d{9}$",
         ErrorMessage = "Enter a valid 10-digit mobile number."),
     Display(Name = "Mobile number")]
    public string Mobile { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required,
     DataType(DataType.Password),
     Compare("Password", ErrorMessage = "Passwords do not match."),
     Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Buyer";

    [StringLength(80), Display(Name = "Farm location (farmers)")]
    public string? Location { get; set; }
}

public class LoginVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class ListingForm : IValidatableObject
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a produce"),
     Display(Name = "Produce")]
    public int ProduceId { get; set; }

    [StringLength(80)]
    public string? Variety { get; set; }

    [Required,
     RegularExpression("^[ABC]$", ErrorMessage = "Grade must be A, B or C")]
    public string Grade { get; set; } = "A";

    [Range(0.01, 1000000,
        ErrorMessage = "Quantity must be greater than zero"),
     Display(Name = "Quantity available")]
    public decimal QuantityAvailable { get; set; }

    [Required, RegularExpression("^(kg|quintal|ton|dozen|piece)$")]
    public string Unit { get; set; } = "kg";

    [Range(0.01, 1000000,
        ErrorMessage = "Minimum order must be greater than zero"),
     Display(Name = "Minimum order quantity")]
    public decimal MinOrderQty { get; set; }

    [Range(0.01, 1000000,
        ErrorMessage = "Price must be greater than zero"),
     Display(Name = "Price per unit (₹)")]
    public decimal PricePerUnit { get; set; }

    [Required,
     DataType(DataType.Date),
     DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}",
         ApplyFormatInEditMode = true),
     Display(Name = "Harvest date")]
    public DateTime? HarvestDate { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(80)]
    public string Location { get; set; } = string.Empty;

    public IFormFile? Image { get; set; }

    public string? ExistingImage { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (MinOrderQty > QuantityAvailable)
        {
            yield return new ValidationResult(
                "Minimum order cannot exceed available quantity.",
                new[] { nameof(MinOrderQty) });
        }

        if (HarvestDate.HasValue)
        {
            var date = HarvestDate.Value.Date;

            if (date > DateTime.Today.AddDays(7) ||
                date < DateTime.Today.AddDays(-365))
            {
                yield return new ValidationResult(
                    "Harvest date must be within the last year and at most 7 days ahead.",
                    new[] { nameof(HarvestDate) });
            }
        }
    }
}

public class EarningsVm
{
    public string Period { get; set; } = string.Empty;

    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public List<Order> Orders { get; set; } = new();

    public List<SellingExpense> Expenses { get; set; } = new();

    public decimal Gross { get; set; }

    public decimal Qty { get; set; }

    public decimal Costs { get; set; }

    public decimal Net { get; set; }
}

public class UserRow
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool Locked { get; set; }
}

public class AdminVm
{
    public List<UserRow> Users { get; set; } = new();

    public List<Listing> Listings { get; set; } = new();

    public List<Order> Orders { get; set; } = new();

    public decimal CompletedRevenue { get; set; }
}
