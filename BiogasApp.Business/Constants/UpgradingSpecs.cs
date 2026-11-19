using BiogasApp.Database.Enums;

namespace BiogasApp.Business.Constants;

public record UpgradingSpec(double Recovery, double Purity, double PowerKwhPerNm3);

public static class UpgradingSpecs
{
    public static readonly IReadOnlyDictionary<UpgradingTech, UpgradingSpec> Specs =
        new Dictionary<UpgradingTech, UpgradingSpec>
        {
            [UpgradingTech.Psa] = new UpgradingSpec(Recovery: 0.98, Purity: 0.96, PowerKwhPerNm3: 0.22),
            [UpgradingTech.Membrane] = new UpgradingSpec(Recovery: 0.975, Purity: 0.97, PowerKwhPerNm3: 0.28),
            [UpgradingTech.Hpws] = new UpgradingSpec(Recovery: 0.96, Purity: 0.95, PowerKwhPerNm3: 0.32)
        };

    public static UpgradingSpec Get(UpgradingTech tech) => Specs[tech];
}