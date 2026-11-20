using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Engine;

public record DigestateBomResult(
    double DigestateSolidCakeTpd,
    double DigestateLiquidLfomTpd,
    double CivilExcavationM3,
    double CivilRccConcreteM3,
    double StructuralSteelRebarTons);

public class DigestateBomService
{
    private const double VsDestructionFraction = 0.55;

    public DigestateBomResult Calculate(
        double qSlurryM3Day,
        double blendedTsPct,
        double diameterM,
        double totalHeightM,
        int numTanks,
        double blendedVsPctTs = 0.0)
    {
        if (qSlurryM3Day <= 0 || diameterM <= 0 || totalHeightM <= 0 || numTanks < 1)
        {
            throw new ArgumentException("Slurry flow, tank dimensions and tank count must be greater than zero.");
        }

        if (blendedTsPct <= 0 || blendedTsPct > 100 || blendedVsPctTs < 0 || blendedVsPctTs > 100)
        {
            throw new ArgumentException("Blended TS and VS percentages must be within valid ranges.");
        }

        // Digestate mass balance
        var qDigestateM3Day = qSlurryM3Day * EngineeringConstants.DigestateFlowFactor;
        var tsRetainedFraction = blendedVsPctTs > 0
            ? 1.0 - ((blendedVsPctTs / 100.0) * VsDestructionFraction)
            : EngineeringConstants.DigestateTsRetainedFraction;
        var tsDig = blendedTsPct * tsRetainedFraction;
        var mDig = qDigestateM3Day * EngineeringConstants.DigestateDensityTPerM3;
        var sDig = mDig * (tsDig / 100.0);

        // Screw press: M_solid = (S_dig - f_liquid * M_dig) / (f_solid - f_liquid)
        var mSolidCakeTpd = (sDig - EngineeringConstants.LiquidFiltrateTsFraction * mDig) /
                            (EngineeringConstants.SolidCakeTsFraction - EngineeringConstants.LiquidFiltrateTsFraction);
        mSolidCakeTpd = Math.Clamp(mSolidCakeTpd, 0.0, mDig);
        var mLiquidLfomTpd = mDig - mSolidCakeTpd;

        // Civil quantities
        var radius = diameterM / 2.0;
        var wallThickness = EngineeringConstants.RccWallThicknessM;
        var raftThickness = EngineeringConstants.RaftThicknessM;

        var excavationVolM3 = Math.PI *
                              Math.Pow(radius + EngineeringConstants.ExcavationOffsetM, 2) *
                              EngineeringConstants.ExcavationDepthM * numTanks;

        var raftConcreteM3 = Math.PI *
                             Math.Pow(radius + EngineeringConstants.RaftOffsetM, 2) *
                             raftThickness * numTanks;

        var wallConcreteM3 = Math.PI *
                             (Math.Pow(radius + wallThickness, 2) - Math.Pow(radius, 2)) *
                             totalHeightM * numTanks;

        var totalConcreteM3 = raftConcreteM3 + wallConcreteM3;
        var steelTonnageTons = (totalConcreteM3 * EngineeringConstants.RebarKgPerM3Concrete) / 1000.0;

        return new DigestateBomResult(
            DigestateSolidCakeTpd: mSolidCakeTpd,
            DigestateLiquidLfomTpd: mLiquidLfomTpd,
            CivilExcavationM3: Math.Ceiling(excavationVolM3),
            CivilRccConcreteM3: Math.Ceiling(totalConcreteM3),
            StructuralSteelRebarTons: Math.Ceiling(steelTonnageTons));
    }
}