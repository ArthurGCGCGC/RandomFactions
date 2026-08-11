using RimWorld;

namespace RandomFactions.Filters;

public class NaturalEnemyFactionFilter(bool isNaturalEnemy) : FactionFilter
{
    protected override bool Matches(Faction f)
    {
        return f.def.naturalEnemy == isNaturalEnemy;
    }
}