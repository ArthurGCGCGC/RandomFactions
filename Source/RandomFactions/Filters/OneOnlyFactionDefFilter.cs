using RimWorld;

namespace RandomFactions.Filters;

public class OneOnlyFactionDefFilter(bool isOneOnly) : FactionDefFilter
{
    protected override bool Matches(FactionDef def)
    {
        return def.maxConfigurableAtWorldCreation  == 1 == isOneOnly;
    }
}