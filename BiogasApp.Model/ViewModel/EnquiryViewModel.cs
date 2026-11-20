using System.ComponentModel.DataAnnotations;
using BiogasApp.Database.Enums;

namespace BiogasApp.Model.ViewModel;

public class EnquiryFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Required, EmailAddress, StringLength(200)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "City")]
    public string LocationCity { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "State")]
    public string LocationState { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Country")]
    public string LocationCountry { get; set; } = "India";

    [Required, StringLength(200)]
    [Display(Name = "Feedstock Type")]
    public string FeedstockType { get; set; } = string.Empty;

    [Range(0.1, 100000)]
    [Display(Name = "Feedstock Quantity (TPD)")]
    public decimal FeedstockQtyTpd { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "End Use")]
    public string EndUse { get; set; } = "CBG";

    [StringLength(2000)]
    [Display(Name = "Message")]
    public string? Message { get; set; }

    [Display(Name = "Source")]
    public EnquirySource Source { get; set; } = EnquirySource.Website;
}

public class EnquiryDetailsViewModel
{
    public Guid Id { get; set; }
    public string EnquiryNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LocationCity { get; set; } = string.Empty;
    public string LocationState { get; set; } = string.Empty;
    public string LocationCountry { get; set; } = string.Empty;
    public string FeedstockType { get; set; } = string.Empty;
    public decimal FeedstockQtyTpd { get; set; }
    public string EndUse { get; set; } = string.Empty;
    public string? Message { get; set; }
    public EnquirySource Source { get; set; }
    public EnquiryStatus Status { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EnquiryListItemViewModel
{
    public Guid Id { get; set; }
    public string EnquiryNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string LocationCity { get; set; } = string.Empty;
    public string FeedstockType { get; set; } = string.Empty;
    public decimal FeedstockQtyTpd { get; set; }
    public string EndUse { get; set; } = string.Empty;
    public EnquirySource Source { get; set; }
    public EnquiryStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EnquiryIndexViewModel
{
    public EnquiryStatus? StatusFilter { get; set; }
    public string? SearchText { get; set; }
    public List<EnquiryListItemViewModel> Items { get; set; } = new();
}

public class EnquiryStatusUpdateViewModel
{
    public Guid Id { get; set; }
    public EnquiryStatus Status { get; set; }

    [StringLength(2000)]
    public string? AdminNotes { get; set; }
}
