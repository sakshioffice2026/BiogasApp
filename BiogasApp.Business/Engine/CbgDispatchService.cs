using BiogasApp.Business.Constants;

namespace BiogasApp.Business.Engine;

public record CbgDispatchResult(
    double CbgTonnesPerDay,
    double CbgTonnesPerYear,
    double TankerPayloadKg,
    int TankerLoadsPerDay,
    double FillTimeHoursPerTanker,
    double BufferHoursAvailable);

public class CbgDispatchService
{
    public CbgDispatchResult Calculate(
        double mCbgKgDay,
        double cascadeWaterCapacityLiters)
    {
        var payloadKg = EngineeringConstants.CbgTankerPayloadKg;

        var tonnesPerDay = mCbgKgDay / 1000.0;
        var tonnesPerYear = tonnesPerDay * EngineeringConstants.PlantOperatingDaysPerYear;

        var cbgKgPerHour = mCbgKgDay / 24.0;

        var loadsPerDay = payloadKg > 0
            ? (int)Math.Ceiling(mCbgKgDay / payloadKg)
            : 0;

        var fillTimeHours = cbgKgPerHour > 0
            ? payloadKg / cbgKgPerHour
            : 0.0;

        var storedKg =
            cascadeWaterCapacityLiters / 1000.0 *
            EngineeringConstants.CascadeStoredKgPerM3Water;

        var bufferHours = cbgKgPerHour > 0
            ? storedKg / cbgKgPerHour
            : 0.0;

        return new CbgDispatchResult(
            CbgTonnesPerDay: tonnesPerDay,
            CbgTonnesPerYear: tonnesPerYear,
            TankerPayloadKg: payloadKg,
            TankerLoadsPerDay: loadsPerDay,
            FillTimeHoursPerTanker: fillTimeHours,
            BufferHoursAvailable: bufferHours);
    }
}
