using BiogasApp.Database.Enums;

namespace BiogasApp.Database.Entities;

public class Feedstock
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FeedstockCategory Category { get; set; }
    public decimal TsPercent { get; set; }
    public decimal VsPercentOfTs { get; set; }
    public decimal CnRatio { get; set; }
    public decimal BmpNm3PerKgVs { get; set; }
    public decimal BulkDensityKgM3 { get; set; }
    public decimal DegradationFactor { get; set; } = 0.850m;
    public decimal DefaultCostPerTon { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectFeedstockMix> ProjectMixes { get; set; } = new List<ProjectFeedstockMix>();
}