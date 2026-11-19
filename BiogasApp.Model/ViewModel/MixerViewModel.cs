using System.ComponentModel.DataAnnotations;

namespace BiogasApp.Model.ViewModel;

public class MixerViewModel
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    [Range(1, 30)]
    [Display(Name = "Target Slurry TS (%)")]
    public double TargetSlurryTsPercent { get; set; } = 9.0;

    public List<MixerRowViewModel> Rows { get; set; } = new();
    public List<FeedstockViewModel> AvailableFeedstocks { get; set; } = new();

    // Real-time gauges
    public double TotalFeedTpd { get; set; }
    public double BlendedTsPercent { get; set; }
    public double BlendedCnRatio { get; set; }
    public string CnGaugeZone { get; set; } = "Green";
    public List<string> Warnings { get; set; } = new();
}

public class MixerRowViewModel
{
    public Guid FeedstockId { get; set; }
    public string FeedstockName { get; set; } = string.Empty;

    [Range(0, 999999.99)]
    [Display(Name = "Daily Tonnage (TPD)")]
    public decimal DailyTonnageTpd { get; set; }

    [Range(1, 365)]
    [Display(Name = "Seasonal Availability (days)")]
    public int SeasonalAvailabilityDays { get; set; } = 365;

    [Range(0, 100)]
    [Display(Name = "Custom TS (%)")]
    public decimal? CustomTsOverride { get; set; }

    [Range(0, 100)]
    [Display(Name = "Custom VS (%)")]
    public decimal? CustomVsOverride { get; set; }
}