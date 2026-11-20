namespace BiogasApp.Model.DTOs;

public class MassBalanceDto
{
    public double TotalFeedTpd { get; set; }
    public double BlendedTsPercent { get; set; }
    public double DilutionWaterRequiredTpd { get; set; }
    public double DilutionWaterM3Day { get; set; }
    public double TotalSlurryFlowM3Day { get; set; }
    public double BlendedCnRatio { get; set; }
    public double VsTotalKgDay { get; set; }
}