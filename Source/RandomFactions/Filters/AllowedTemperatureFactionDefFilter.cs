using RimWorld;

namespace RandomFactions.Filters;

public class AllowedTemperatureFactionDefFilter(float temperature) : FactionDefFilter
{
    protected override bool Matches(FactionDef f)
    {
        return f.allowedArrivalTemperatureRange.Includes(temperature);
    }
}