using RimWorld;

namespace RandomFactions.Filters;

public class NaturalEnemyFactionDefFilter(bool isNaturalEnemy) : FactionDefFilter
{
    protected override bool Matches(FactionDef f)
    {
        return f.naturalEnemy == isNaturalEnemy;
    }
}