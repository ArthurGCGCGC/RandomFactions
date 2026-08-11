using RimWorld;

namespace RandomFactions.Filters;

public class HiddenFactionDefFilter(bool isHidden) : FactionDefFilter
{
    protected override bool Matches(FactionDef f)
    {
        return f.hidden == isHidden;
    }
}