using System.ComponentModel.DataAnnotations;
using BiogasApp.Database.Enums;

namespace BiogasApp.Model.ViewModel;

public class ProjectFormViewModel : IValidatableObject
{
    public Guid? Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Project Name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Location (Country)")]
    public string LocationCountry { get; set; } = string.Empty;

    [Display(Name = "Regulatory Code")]
    public RegulatoryStandard RegulatoryStandard { get; set; } = RegulatoryStandard.PesoIndia;

    [Range(1, 10000)]
    [Display(Name = "Plot Length X (m)")]
    public double PlotLengthM { get; set; }

    [Range(1, 10000)]
    [Display(Name = "Plot Width Y (m)")]
    public double PlotWidthM { get; set; }

    [Range(-50, 60)]
    [Display(Name = "Ambient Temp Min (°C)")]
    public decimal AmbientTempMinC { get; set; } = 10m;

    [Range(-50, 60)]
    [Display(Name = "Ambient Temp Max (°C)")]
    public decimal AmbientTempMaxC { get; set; } = 40m;

    [Display(Name = "Digester Regime")]
    public DigesterRegime DigesterRegime { get; set; } = DigesterRegime.Mesophilic;

    [Display(Name = "Upgrading Technology")]
    public UpgradingTech UpgradingTech { get; set; } = UpgradingTech.Psa;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Id.HasValue && PlotLengthM * PlotWidthM < 2500)
        {
            yield return new ValidationResult(
                "Plot area must be at least 2500 m².",
                new[] { nameof(PlotLengthM), nameof(PlotWidthM) });
        }

        if (AmbientTempMinC >= AmbientTempMaxC)
        {
            yield return new ValidationResult(
                "Minimum ambient temperature must be lower than the maximum.",
                new[] { nameof(AmbientTempMinC), nameof(AmbientTempMaxC) });
        }
    }
}