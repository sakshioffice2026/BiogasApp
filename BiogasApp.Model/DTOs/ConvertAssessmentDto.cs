namespace BiogasApp.Model.DTOs;

public class ConvertAssessmentDto
{
    public Guid AssessmentId { get; set; }

    public string ProjectName { get; set; } = string.Empty;
    public string LocationCountry { get; set; } = "India";

    // PesoIndia or AtexEu
    public string RegulatoryStandard { get; set; } = "PesoIndia";

    public decimal AmbientTempMinC { get; set; } = 5;
    public decimal AmbientTempMaxC { get; set; } = 40;

    // Mesophilic or Thermophilic
    public string DigesterRegime { get; set; } = "Mesophilic";

    // Psa, Membrane or Hpws
    public string UpgradingTech { get; set; } = "Psa";
}
