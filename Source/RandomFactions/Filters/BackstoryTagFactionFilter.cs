using System.Linq;
using RimWorld;

namespace RandomFactions.Filters;

public class BackstoryTagFactionFilter(BackstoryCategoryFilter tag) : FactionFilter
{
    // eg "Offworld" or "Pirate"

    protected override bool Matches(Faction f)
    {
        return f.def.backstoryFilters.SelectMany(tag1 => tag1.categories).Any(cat => cat == tag.ToString());
    }
}