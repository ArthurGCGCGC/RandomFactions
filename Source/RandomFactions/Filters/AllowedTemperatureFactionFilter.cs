using RimWorld;

namespace RandomFactions.Filters;

public class AllowedTemperatureFactionFilter(float temperature) : FactionFilter
{
    protected override bool Matches(Faction f)
    {
        return f.def.allowedArrivalTemperatureRange.Includes(temperature);
    }
}