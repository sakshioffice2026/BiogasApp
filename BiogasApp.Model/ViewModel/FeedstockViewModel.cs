using System.ComponentModel.DataAnnotations;
using BiogasApp.Database.Enums;

namespace BiogasApp.Model.ViewModel;

public class FeedstockViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public FeedstockCategory Category { get; set; } = FeedstockCategory.Agro;

    [Range(0, 100)]
    [Display(Name = "TS (%)")]
    public decimal TsPercent { get; set; }

    [Range(0, 100)]
    [Display(Name = "VS (% of TS)")]
    public decimal VsPercentOfTs { get; set; }

    [Range(0.01, 999.99)]
    [Display(Name = "C:N Ratio")]
    public decimal CnRatio { get; set; }

    [Range(0.0001, 99.9999)]
    [Display(Name = "BMP (Nm³/kg VS)")]
    public decimal BmpNm3PerKgVs { get; set; }

    [Range(0.01, 99999.99)]
    [Display(Name = "Bulk Density (kg/m³)")]
    public decimal BulkDensityKgM3 { get; set; }

    [Range(0, 1)]
    [Display(Name = "Degradation Factor")]
    public decimal DegradationFactor { get; set; } = 0.850m;

    [Range(0, 99999999.99)]
    [Display(Name = "Default Cost (per ton)")]
    public decimal DefaultCostPerTon { get; set; }
}

public class FeedstockIndexViewModel
{
    public string? Search { get; set; }
    public FeedstockCategory? CategoryFilter { get; set; }
    public List<FeedstockViewModel> Feedstocks { get; set; } = new();
}