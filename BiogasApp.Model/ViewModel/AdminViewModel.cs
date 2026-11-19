using System.ComponentModel.DataAnnotations;

namespace BiogasApp.Model.ViewModel;

public class AdminViewModel
{
    public List<AdminUserViewModel> Users { get; set; } = new();
    public List<RateCardViewModel> RateCards { get; set; } = new();
    public List<ExceptionLogItemViewModel> RecentExceptions { get; set; } = new();
}

public class AdminUserViewModel
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RateCardViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Item Code")]
    public string ItemCode { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = "Civil";

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Unit { get; set; } = "nos";

    [Range(0, 99999999.99)]
    [Display(Name = "Unit Rate")]
    public decimal UnitRate { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "INR";
}

public class ExceptionLogItemViewModel
{
    public long Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? RequestPath { get; set; }
    public DateTime CreatedAt { get; set; }
}