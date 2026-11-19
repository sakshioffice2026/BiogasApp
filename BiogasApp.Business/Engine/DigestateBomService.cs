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
    public DigestateBomResult Calculate(
        double qSlurryM3Day,
        double blendedTsPct,
        double diameterM,
        double totalHeightM,
        int numTanks)
    {
        // Digestate mass balance
        var qDigestateM3Day = qSlurryM3Day * EngineeringConstants.DigestateFlowFactor;
        var tsDig = blendedTsPct * EngineeringConstants.DigestateTsRetainedFraction;
        var mDig = qDigestateM3Day * EngineeringConstants.DigestateDensityTPerM3;
        var sDig = mDig * (tsDig / 100.0);

        // Screw press: M_solid = (S_dig - 0.03 * M_dig) / (0.30 - 0.03)
        var mSolidCakeTpd = (sDig - EngineeringConstants.LiquidFiltrateTsFraction * mDig) /
                            (EngineeringConstants.SolidCakeTsFraction - EngineeringConstants.LiquidFiltrateTsFraction);
        mSolidCakeTpd = Math.Max(mSolidCakeTpd, 0.0);
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