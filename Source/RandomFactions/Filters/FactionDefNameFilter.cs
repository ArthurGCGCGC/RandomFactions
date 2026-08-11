using System.Collections.Generic;
using RimWorld;

namespace RandomFactions.Filters;

public class FactionDefNameFilter : FactionDefFilter
{
    private readonly bool include;
    private readonly HashSet<string> names;

    public FactionDefNameFilter(params string[] names) : this(true, names)
    {
    }

    public FactionDefNameFilter(bool include, params string[] names)
    {
        this.names = [];
        foreach (var n in names)
        {
            this.names.Add(n);
        }

        this.include = include;
    }

    protected override bool Matches(FactionDef f)
    {
        return include == names.Contains(f.defName);
    }
}