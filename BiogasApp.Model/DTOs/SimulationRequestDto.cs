namespace BiogasApp.Model.DTOs;

public class SimulationRequestDto
{
    public Guid ProjectId { get; set; }
    public double TargetSlurryTsPercent { get; set; } = 9.0;
    public string DigesterRegime { get; set; } = "MESOPHILIC";
    public string UpgradingTechnology { get; set; } = "PSA";
    public List<FeedstockMixItemDto> FeedstockMix { get; set; } = new();
}

public class FeedstockMixItemDto
{
    public Guid FeedstockId { get; set; }
    public double DailyTonnageTpd { get; set; }
    public double? CustomTsOverride { get; set; }
    public double? CustomVsOverride { get; set; }
}