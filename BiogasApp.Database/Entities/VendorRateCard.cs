namespace BiogasApp.Database.Entities;

public class VendorRateCard
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal UnitRate { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
}