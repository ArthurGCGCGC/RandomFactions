using RimWorld;

namespace RandomFactions.Filters;

public class DefeatedFactionFilter(bool isDefeated) : FactionFilter
{
    protected override bool Matches(Faction f)
    {
        return f.defeated == isDefeated;
    }
}