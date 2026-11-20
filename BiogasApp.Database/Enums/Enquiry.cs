using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class Enquiry
{
    public Guid Id { get; set; }
    public string EnquiryNo { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string LocationCity { get; set; } = string.Empty;
    public string LocationState { get; set; } = string.Empty;
    public string LocationCountry { get; set; } = "India";

    public string FeedstockType { get; set; } = string.Empty;
    public decimal FeedstockQtyTpd { get; set; }
    public string EndUse { get; set; } = string.Empty;
    public string? Message { get; set; }

    public EnquirySource Source { get; set; } = EnquirySource.Website;
    public EnquiryStatus Status { get; set; } = EnquiryStatus.New;

    public string? AdminNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
