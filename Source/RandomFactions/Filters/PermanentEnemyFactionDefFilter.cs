using RimWorld;

namespace RandomFactions.Filters;

public class PermanentEnemyFactionDefFilter(bool isPermanentEnemy) : FactionDefFilter
{
    protected override bool Matches(FactionDef f)
    {
        return f.permanentEnemy == isPermanentEnemy;
    }
}