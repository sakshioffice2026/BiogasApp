using System.ComponentModel.DataAnnotations;
using BiogasApp.Database.Enums;

namespace BiogasApp.Model.ViewModel;

public class EnquiryInviteFormViewModel
{
    [Required]
    [StringLength(150)]
    [Display(Name = "Customer name")]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(200)]
    [Display(Name = "Email")]
    public string? Email { get; set; }
}

public class EnquiryInviteListItemViewModel
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public InviteStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? EnquiryId { get; set; }
    public string? EnquiryNo { get; set; }

    /// <summary>Full public link, filled in by the controller.</summary>
    public string Link { get; set; } = string.Empty;
}

public class EnquiryInviteIndexViewModel
{
    public EnquiryInviteFormViewModel Form { get; set; } = new();
    public List<EnquiryInviteListItemViewModel> Invites { get; set; } = new();

    /// <summary>Generic public enquiry form link (no token).</summary>
    public string GenericLink { get; set; } = string.Empty;
}

public class EnquiryInviteValidationResult
{
    public bool IsValid { get; set; }
    public string? CustomerName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
