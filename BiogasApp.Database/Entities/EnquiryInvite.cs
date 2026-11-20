using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class EnquiryInvite
{
    public Guid Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public InviteStatus Status { get; set; } = InviteStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime? SubmittedAt { get; set; }

    public Guid? EnquiryId { get; set; }
    public Enquiry? Enquiry { get; set; }
}
