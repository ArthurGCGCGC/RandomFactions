using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RandomFactions;

public class Settings : ModSettings
{
    public string _factionBlacklist = string.Empty;
    public bool allowDuplicates;
    public bool removeOtherFactions = true;
    public int xenoPercent = 65;

    public HashSet<string> FactionBlacklist { get; private set; } = [];

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref removeOtherFactions, "removeOtherFactions", true);
        Scribe_Values.Look(ref xenoPercent, "PercentXenotype", 40);
        Scribe_Values.Look(ref allowDuplicates, "allowDuplicates");
        Scribe_Values.Look(ref _factionBlacklist, "factionBlacklist");

        FactionBlacklist = _factionBlacklist
            .Split(',')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}