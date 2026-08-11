using RimWorld;

namespace RandomFactions.Filters;

public class PlayerFactionDefFilter(bool isPlayer) : FactionDefFilter
{
    protected override bool Matches(FactionDef f)
    {
        return f.isPlayer == isPlayer;
    }
}