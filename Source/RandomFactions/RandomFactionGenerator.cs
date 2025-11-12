/*
# Random Factions Rimworld Mod
Author: Dr. Plantabyte (aka Christopher C. Hall)
## CC BY 4.0

This work is licensed on the [Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/) Creative Commons License.


### You are free to:

* **Share** — copy and redistribute the material in any medium or format
* **Adapt** — remix, transform, and build upon the material
    for any purpose, even commercially.


### Under the following terms:

* **Attribution** — You must give appropriate credit, provide a link to the license, and indicate if changes were made. You may do so in any reasonable manner, but not in any way that suggests the licensor endorses you or your use.

* **No additional restrictions** — You may not apply legal terms or technological measures that legally restrict others from doing anything the license permits.

### Guarentees:

The licensor cannot revoke these freedoms as long as you follow the license terms.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib.Utils;
using RandomFactions;
using RandomFactions.filters;
using RimWorld;
using Verse;

public class RandomFactionGenerator
{
    private readonly List<FactionDef> definedFactionDefs;

    private readonly bool hasBiotech;
    private readonly ModLogger modLogger;
    private readonly string[] modOffBooksFactionDefNames;
    private readonly int percentXeno;
    private readonly Random prng;

    private readonly List<XenotypeDef> violenceCapableNonBaselineXenotypes;

    public RandomFactionGenerator(int percentXenoFaction, IEnumerable<FactionDef> allFactionDefs,
        string[] offBooksFactionDefNames, bool hasBiotechExpansion,
        List<XenotypeDef> violenceCapableNonBaselineXenotypes, ModLogger logger)
    {
        // init globals
        modLogger = logger;
        percentXeno = percentXenoFaction;
        hasBiotech = hasBiotechExpansion;
        modOffBooksFactionDefNames = offBooksFactionDefNames;
        var seeder = new Random(Find.World.ConstantRandSeed);
        var seedBuffer = new byte[4];
        seeder.NextBytes(seedBuffer);
        var seed = BitConverter.ToInt32(seedBuffer, 0);
        prng = new Random(seed);
        this.violenceCapableNonBaselineXenotypes = violenceCapableNonBaselineXenotypes;

        // load existing faction definitions except the ones from this mod
        definedFactionDefs = allFactionDefs
            .Where(x => !x.categoryTag.EqualsIgnoreCase(RandomFactionsMod.RandomCategoryName)).ToList();

        logger.Trace($"RandomFactionGenerator constructed with random number seed {Find.World.ConstantRandSeed}");
    }

    private void ReplaceWithRandomFaction(Faction faction, bool allowDuplicates,
        Func<Faction[], bool, Faction> randomFactionSelector)
    {
        var priorFactions = Find.World.factionManager.AllFactions;
        var existingFactions = priorFactions as Faction[] ?? priorFactions.ToArray();

        var newFaction = randomFactionSelector(existingFactions, allowDuplicates);
        if (newFaction == null)
        {
            modLogger.Message($"Failed to generate a new faction to replace {faction}. Retaining the old faction.");
            return;
        }

        ReplaceFaction(faction, newFaction);
    }

    public void ReplaceWithRandomNonHiddenFaction(Faction faction, bool allowDuplicates)
    {
        ReplaceWithRandomFaction(faction, allowDuplicates, GetRandomNpcFaction);
    }

    public void ReplaceWithRandomNonHiddenEnemyFaction(Faction faction, bool allowDuplicates)
    {
        ReplaceWithRandomFaction(faction, allowDuplicates, GetRandomEnemyFaction);
    }

    public void ReplaceWithRandomNonHiddenWarlordFaction(Faction faction, bool allowDuplicates)
    {
        ReplaceWithRandomFaction(faction, allowDuplicates, GetRandomRoughFaction);
    }

    public void ReplaceWithRandomNonHiddenTraderFaction(Faction faction, bool allowDuplicates)
    {
        ReplaceWithRandomFaction(faction, allowDuplicates, GetRandomNeutralFaction);
    }

    public void ReplaceWithRandomNamedFaction(Faction faction, bool allowDuplicates, params string[] validDefNames)
    {
        ReplaceWithRandomFaction(faction, allowDuplicates, (x, y) => GetRandomNamedFaction(x, y, validDefNames));
    }

    private void ReplaceFaction(Faction oldFaction, Faction newFaction)
    {
        modLogger.Message(
            $"Replacing faction {oldFaction.Name} ({oldFaction.def.defName}) with faction {newFaction.Name} ({newFaction.def.defName})");

        foreach (var stl in Find.WorldObjects.Settlements)
        {
            if (stl.Faction.Equals(oldFaction))
            {
                stl.SetFaction(newFaction);
            }
        }

        oldFaction.defeated = true;
        oldFaction.hidden = true;
        Find.World.factionManager.Add(newFaction);
    }

    private static int GetFactionsOfTypeCount(FactionDef def, IEnumerable<Faction> factions)
    {
        return factions.Count(f => f.def.defName == def.defName);
    }

    private FactionDef GetRandomFactionDef(List<FactionDef> factionDefs, Faction[] existingFactions)
    {
        FactionDef randomFactionDef;

        var limit = 100;

        do
        {
            randomFactionDef = factionDefs[prng.Next(factionDefs.Count)];

            var count = GetFactionsOfTypeCount(randomFactionDef, existingFactions);
            if (randomFactionDef.maxCountAtGameStart <= 0 || count < randomFactionDef.maxCountAtGameStart)
            {
                break;
            }
        } while (--limit > 0);

        var factionIsPatchable = RandomFactionsMod.IsFactionXenotypePatchable(randomFactionDef);

        modLogger.Trace($"{randomFactionDef.defName} is patchable: {factionIsPatchable}.");

        if (!hasBiotech || !factionIsPatchable || !Rand.Chance(percentXeno / 100f))
        {
            modLogger.Trace($"Skipping baseliner xenotype replacement for faction {randomFactionDef.defName}");
            return randomFactionDef;
        }

        modLogger.Message($"Replacing baseliner xenotype for faction {randomFactionDef.defName}");

        var randomXenotypeDef = GetRandomNonBaselineXenotypeDef();
        var xenoFactionDefName = RandomFactionsMod.GetXenoFactionDefName(randomXenotypeDef, randomFactionDef);
        var factionDef = FindFactionDefByName(xenoFactionDefName);

        if (factionDef != null)
        {
            Messages.Message("RaFa.baseLineReplaced".Translate(randomXenotypeDef.label), MessageTypeDefOf.NeutralEvent);
            return factionDef;
        }

        modLogger.Warning(
            $"Couldn't replace baseliner xenotype for faction {randomFactionDef.defName} using xenotype {randomXenotypeDef.defName}");
        return randomFactionDef;
    }

    private XenotypeDef GetRandomNonBaselineXenotypeDef()
    {
        return violenceCapableNonBaselineXenotypes[prng.Next(violenceCapableNonBaselineXenotypes.Count)];
    }

    private static FactionDef FindFactionDefByName(string name)
    {
        return DefDatabase<FactionDef>.AllDefs.FirstOrDefault(def => name.Equals(def.defName));
    }

    private Faction GetRandomNpcFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        while (true)
        {
            var filteredDefs = FactionDefFilter.FilterFactionDefs(definedFactionDefs, new PlayerFactionDefFilter(false),
                new HiddenFactionDefFilter(false), new FactionDefNameFilter(false, modOffBooksFactionDefNames),
                GetDuplicatesFilter(existingFactions, allowDuplicates));

            // if there's already one of everything, allow duplicates again
            if (filteredDefs.Count == 0)
            {
                if (allowDuplicates)
                {
                    return null;
                }

                allowDuplicates = true;
                continue;
            }

            var randomFactionDef = GetRandomFactionDef(filteredDefs, existingFactions);
            return GenerateFactionFromDef(randomFactionDef, existingFactions);
        }
    }

    private Faction GetRandomEnemyFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        while (true)
        {
            var filteredDefs = FactionDefFilter.FilterFactionDefs(definedFactionDefs, new PlayerFactionDefFilter(false),
                new HiddenFactionDefFilter(false), new FactionDefNameFilter(false, modOffBooksFactionDefNames),
                new PermanentEnemyFactionDefFilter(true), GetDuplicatesFilter(existingFactions, allowDuplicates));

            // if there's already one of everything, allow duplicates again
            if (filteredDefs.Count == 0)
            {
                if (allowDuplicates)
                {
                    return null;
                }

                allowDuplicates = true;
                continue;
            }

            var randomFactionDef = GetRandomFactionDef(filteredDefs, existingFactions);
            return GenerateFactionFromDef(randomFactionDef, existingFactions);
        }
    }

    private Faction GetRandomRoughFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        while (true)
        {
            var filteredDefs = FactionDefFilter.FilterFactionDefs(definedFactionDefs, new PlayerFactionDefFilter(false),
                new HiddenFactionDefFilter(false), new FactionDefNameFilter(false, modOffBooksFactionDefNames),
                new PermanentEnemyFactionDefFilter(false), new NaturalEnemyFactionDefFilter(true),
                GetDuplicatesFilter(existingFactions, allowDuplicates));

            // if there's already one of everything, allow duplicates again
            if (filteredDefs.Count == 0)
            {
                if (allowDuplicates)
                {
                    return null;
                }

                allowDuplicates = true;
                continue;
            }

            var randomFactionDef = GetRandomFactionDef(filteredDefs, existingFactions);
            return GenerateFactionFromDef(randomFactionDef, existingFactions);
        }
    }

    private Faction GetRandomNeutralFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        while (true)
        {
            var filteredDefs = FactionDefFilter.FilterFactionDefs(definedFactionDefs, new PlayerFactionDefFilter(false),
                new HiddenFactionDefFilter(false), new FactionDefNameFilter(false, modOffBooksFactionDefNames),
                new PermanentEnemyFactionDefFilter(false), new NaturalEnemyFactionDefFilter(false),
                GetDuplicatesFilter(existingFactions, allowDuplicates));

            // if there's already one of everything, allow duplicates again
            if (filteredDefs.Count == 0)
            {
                if (allowDuplicates)
                {
                    return null;
                }

                allowDuplicates = true;
                continue;
            }

            var randomFactionDef = GetRandomFactionDef(filteredDefs, existingFactions);
            return GenerateFactionFromDef(randomFactionDef, existingFactions);
        }
    }

    private Faction GetRandomNamedFaction(Faction[] existingFactions, bool allowDuplicates, params string[] nameList)
    {
        while (true)
        {
            var filteredDefs = FactionDefFilter.FilterFactionDefs(definedFactionDefs, new PlayerFactionDefFilter(false),
                new FactionDefNameFilter(false, modOffBooksFactionDefNames), new FactionDefNameFilter(nameList),
                GetDuplicatesFilter(existingFactions, allowDuplicates));

            // if there's already one of everything, allow duplicates again
            if (filteredDefs.Count == 0)
            {
                if (allowDuplicates)
                {
                    return null;
                }

                allowDuplicates = true;
                continue;
            }

            var randomFactionDef = GetRandomFactionDef(filteredDefs, existingFactions);
            return GenerateFactionFromDef(randomFactionDef, existingFactions);
        }
    }

    private Faction GenerateFactionFromDef(FactionDef def, Faction[] existingFactions)
    {
        var relations = GenerateDefaultRelations(def, existingFactions);
        try
        {
            var factionWithRelations = FactionGenerator.NewGeneratedFactionWithRelations(def, relations, def.hidden);
            return factionWithRelations;
        }
        catch (Exception ex)
        {
            modLogger.Error(
                $"Couldn't generate faction with relations from def {def.defName}. Exception: {ex.Message}.");
            return null;
        }
    }

    private static FactionDefFilter GetDuplicatesFilter(Faction[] existingFactions, bool allowDuplicates)
    {
        if (allowDuplicates)
        {
            return new FactionDefNoOpFilter();
        }

        var defNames = existingFactions.Select(x => x.def.defName).ToArray();
        return new FactionDefNameFilter(false, defNames);
    }

    private static FactionRelationKind GetDefaultRelationKind(FactionDef def)
    {
        if (def.permanentEnemy || def.naturalEnemy)
        {
            return FactionRelationKind.Hostile;
        }

        return FactionRelationKind.Neutral;
    }

    private static List<FactionRelation> GenerateDefaultRelations(FactionDef target, Faction[] allFactions)
    {
        var relationList = new List<FactionRelation>();
        foreach (var faction in allFactions)
        {
            if (faction.IsPlayer)
            {
                continue;
            }

            if (GetDefaultRelationKind(target) == FactionRelationKind.Hostile ||
                GetDefaultRelationKind(faction.def) == FactionRelationKind.Hostile)
            {
                relationList.Add(new FactionRelation(faction, FactionRelationKind.Hostile));
            }
            else
            {
                relationList.Add(new FactionRelation(faction, FactionRelationKind.Neutral));
            }
        }

        return relationList;
    }
}