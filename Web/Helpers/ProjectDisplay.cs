using BiogasApp.Database.Enums;

namespace Web.Helpers;

public static class ProjectDisplay
{
    public static string Label(this RegulatoryStandard value) => value switch
    {
        RegulatoryStandard.PesoIndia => "PESO (India)",
        RegulatoryStandard.AtexEu => "ATEX (EU)",
        _ => value.ToString()
    };

    public static string Hint(this RegulatoryStandard value) => value switch
    {
        RegulatoryStandard.PesoIndia => "Petroleum and Explosives Safety Organisation clearance",
        RegulatoryStandard.AtexEu => "European explosive-atmosphere directives",
        _ => string.Empty
    };

    public static string Label(this DigesterRegime value) => value switch
    {
        DigesterRegime.Mesophilic => "Mesophilic",
        DigesterRegime.Thermophilic => "Thermophilic",
        _ => value.ToString()
    };

    public static string Hint(this DigesterRegime value) => value switch
    {
        DigesterRegime.Mesophilic => "Around 37 \u00B0C. Steadier and simpler to run.",
        DigesterRegime.Thermophilic => "Around 55 \u00B0C. Faster breakdown, needs more heat.",
        _ => string.Empty
    };

    public static string Label(this UpgradingTech value) => value switch
    {
        UpgradingTech.Psa => "PSA",
        UpgradingTech.Membrane => "Membrane",
        UpgradingTech.Hpws => "HPWS",
        _ => value.ToString()
    };

    public static string Hint(this UpgradingTech value) => value switch
    {
        UpgradingTech.Psa => "Pressure swing adsorption",
        UpgradingTech.Membrane => "Membrane separation",
        UpgradingTech.Hpws => "High-pressure water scrubbing",
        _ => string.Empty
    };

    public static string Label(this ProjectStatus value) => value switch
    {
        ProjectStatus.Draft => "Draft",
        ProjectStatus.Simulated => "Simulated",
        ProjectStatus.Approved => "Approved",
        ProjectStatus.Archived => "Archived",
        _ => value.ToString()
    };

    public static string ActionLabel(this ProjectStatus value) => value switch
    {
        ProjectStatus.Draft => "Move back to draft",
        ProjectStatus.Simulated => "Mark as simulated",
        ProjectStatus.Approved => "Approve project",
        ProjectStatus.Archived => "Archive project",
        _ => value.ToString()
    };

    public static string Css(this ProjectStatus value) => value.ToString().ToLowerInvariant();

    public static int FillPercent(this ProjectStatus value) => value switch
    {
        ProjectStatus.Draft => 25,
        ProjectStatus.Simulated => 65,
        ProjectStatus.Approved => 100,
        _ => 0
    };

    public static string Label(this FeedstockCategory value) => value switch
    {
        FeedstockCategory.Agro => "Agro residue",
        FeedstockCategory.Manure => "Manure",
        FeedstockCategory.Industrial => "Industrial",
        FeedstockCategory.Msw => "Municipal waste",
        _ => value.ToString()
    };

    public static string Css(this FeedstockCategory value) => value.ToString().ToLowerInvariant();
}
