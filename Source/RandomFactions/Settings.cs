using Verse;

namespace RandomFactions;

public class Settings : ModSettings
{
    public bool allowDuplicates;
    public bool removeOtherFactions = true;
    public int xenoPercent = 40;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref removeOtherFactions, "removeOtherFactions", true);
        Scribe_Values.Look(ref xenoPercent, "PercentXenotype", 40);
        Scribe_Values.Look(ref allowDuplicates, "allowDuplicates");
    }
}