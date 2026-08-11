using RimWorld;
using Verse;

namespace RandomFactions.Filters;

public class CategoryTagFactionFilter(string tag) : FactionFilter
{
    // eg "Outlander"

    protected override bool Matches(Faction f)
    {
        return f.def.categoryTag.EqualsIgnoreCase(tag);
    }
}