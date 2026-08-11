using System.Linq;
using RimWorld;

namespace RandomFactions.Filters;

public class BackstoryTagFactionDefFilter(BackstoryCategoryFilter tag) : FactionDefFilter
{
    // eg "Offworld" or "Pirate"

    protected override bool Matches(FactionDef f)
    {
        return f.backstoryFilters.Any(backstoryCategoryFilter =>
            backstoryCategoryFilter.categories.Any(cat => cat == tag.ToString()));
    }
}