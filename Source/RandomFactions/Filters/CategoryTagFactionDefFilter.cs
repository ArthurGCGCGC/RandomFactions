using RimWorld;
using Verse;

namespace RandomFactions.Filters;

public class CategoryTagFactionDefFilter(string tag) : FactionDefFilter
{
    // eg "Outlander"

    protected override bool Matches(FactionDef f)
    {
        return f.categoryTag.EqualsIgnoreCase(tag);
    }
}