using RimWorld;

namespace RandomFactions.Filters;

public class PermanentEnemyFactionFilter(bool isPermanentEnemy) : FactionFilter
{
    protected override bool Matches(Faction f)
    {
        return f.def.permanentEnemy == isPermanentEnemy;
    }
}