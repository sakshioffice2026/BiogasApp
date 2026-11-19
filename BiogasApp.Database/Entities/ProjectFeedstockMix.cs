namespace BiogasApp.Database.Entities;

public class ProjectFeedstockMix
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FeedstockId { get; set; }
    public decimal DailyTonnageTpd { get; set; }
    public int SeasonalAvailabilityDays { get; set; } = 365;
    public decimal? CustomTsOverride { get; set; }
    public decimal? CustomVsOverride { get; set; }

    public Project? Project { get; set; }
    public Feedstock? Feedstock { get; set; }
}