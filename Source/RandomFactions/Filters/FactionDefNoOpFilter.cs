using RimWorld;

namespace RandomFactions.Filters;

public class FactionDefNoOpFilter : FactionDefFilter
{
    protected override bool Matches(FactionDef f)
    {
        // do nothing
        return true;
    }
}