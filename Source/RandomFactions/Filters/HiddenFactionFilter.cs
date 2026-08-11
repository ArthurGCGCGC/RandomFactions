using RimWorld;

namespace RandomFactions.Filters;

public class HiddenFactionFilter(bool isHidden) : FactionFilter
{
    protected override bool Matches(Faction f)
    {
        return f.Hidden == isHidden;
    }
}