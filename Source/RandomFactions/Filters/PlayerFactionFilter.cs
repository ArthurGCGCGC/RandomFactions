using RimWorld;

namespace RandomFactions.Filters;

public class PlayerFactionFilter(bool isPlayer) : FactionFilter
{
    protected override bool Matches(Faction f)
    {
        return f.IsPlayer == isPlayer;
    }
}