using BiogasApp.Database.Enums;
using BiogasApp.Model.DTOs;

namespace BiogasApp.Model.ViewModel;

public class SimulationDashboardViewModel
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid SimulationRunId { get; set; }
    public int RunVersion { get; set; }
    public ProjectStatus ProjectStatus { get; set; }

    public UpgradingTech SelectedTechnology { get; set; } = UpgradingTech.Psa;

    public DashboardDto Kpis { get; set; } = new();
    public MassBalanceDto MassBalance { get; set; } = new();
    public DigesterKineticsDto DigesterKinetics { get; set; } = new();
    public PreTreatmentDto PreTreatment { get; set; } = new();
    public ThermalMechanicalDto Thermal { get; set; } = new();
    public UpgradingDto Upgrading { get; set; } = new();
    public DigestateCivilDto Digestate { get; set; } = new();
    public CbgDispatchDto Dispatch { get; set; } = new();
    public List<EquipmentSizingDto> Equipment { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}