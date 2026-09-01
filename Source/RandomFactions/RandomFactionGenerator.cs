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
using RandomFactions.Filters;
using RimWorld;
using Verse;

namespace RandomFactions;

public class RandomFactionGenerator
{
    private readonly List<FactionDef> _definedFactionDefs;

    private readonly bool _hasBiotech;
    private readonly string[] _modOffBooksFactionDefNames;
    private readonly int _percentXeno;
    private readonly Random _prng;

    private readonly List<XenotypeDef> _violenceCapableNonBaselineXenotypes;
    private readonly Faction[] _nonBlacklistedFactions;

    public RandomFactionGenerator(int percentXenoFaction, IEnumerable<FactionDef> allFactionDefs,
        string[] offBooksFactionDefNames, bool hasBiotechExpansion,
        List<XenotypeDef> violenceCapableNonBaselineXenotypes,
        HashSet<string> blacklistedFactions)
    {
        // init globals
        _percentXeno = percentXenoFaction;
        _hasBiotech = hasBiotechExpansion;
        _modOffBooksFactionDefNames = offBooksFactionDefNames;
         _nonBlacklistedFactions = Find.World.factionManager.AllFactions.Where(f => !blacklistedFactions.Contains(f.def.defName)).ToArray();

        _prng = new Random(Find.World.ConstantRandSeed);

        this._violenceCapableNonBaselineXenotypes = violenceCapableNonBaselineXenotypes;

        // load existing faction definitions except the ones from this mod
        _definedFactionDefs = allFactionDefs
            .Where(x => !x.categoryTag.EqualsIgnoreCase(RandomFactionsMod.RandomCategoryName) && !blacklistedFactions.Contains(x.defName)).ToList();

        Log.Message($"[RandomFactions] RandomFactionGenerator constructed with random number seed {Find.World.ConstantRandSeed}");
    }

    private void ReplaceWithRandomFaction(Faction faction, bool allowDuplicates,
        Func<Faction[], bool, Faction> randomFactionSelector)
    {
        var newFaction = randomFactionSelector(_nonBlacklistedFactions, allowDuplicates);
        if (newFaction == null)
        {
            Log.Warning($"[RandomFactions] Failed to generate a new faction to replace {faction}. Retaining the old faction.");
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
        Log.Message(
            $"[RandomFactions] Replacing faction {oldFaction.Name} ({oldFaction.def.defName}) with faction {newFaction.Name} ({newFaction.def.defName})");

        foreach (var stl in Find.WorldObjects.Settlements.Where(stl => stl.Faction.Equals(oldFaction)))
        {
            stl.SetFaction(newFaction);
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
            randomFactionDef = factionDefs[_prng.Next(factionDefs.Count)];

            var count = GetFactionsOfTypeCount(randomFactionDef, existingFactions);
            if (randomFactionDef.maxConfigurableAtWorldCreation <= 0 || count < randomFactionDef.maxConfigurableAtWorldCreation)
            {
                break;
            }
        } while (--limit > 0);


        if (!_hasBiotech)
        {
            return randomFactionDef;
        }

        var notPatchableReason = RandomFactionsMod.IsFactionXenotypePatchable(randomFactionDef);
        var isPatchable = notPatchableReason == RandomFactionsMod.XenotypeNotPatchableReason.None;

        Log.Message(
            $"[RandomFactions] {randomFactionDef.defName} is patchable: {(isPatchable ? "<color=green>True</color>" : "<color=red>False</color>")}" +
            (isPatchable ? string.Empty : $" ({notPatchableReason})")
        );

        if (!isPatchable)
        {
            return randomFactionDef;
        }

        if (!Rand.Chance(_percentXeno / 100f))
        {
            Log.Message($"[RandomFactions] Skipping baseliner xenotype replacement for faction {randomFactionDef.defName}: roll missed target chance ({_percentXeno}%).");
            return randomFactionDef;
        }

        Log.Message($"[RandomFactions] Replacing xenotype for faction {randomFactionDef.defName}");

        var randomXenotypeDef = GetRandomNonBaselineXenotypeDef();
        var xenoFactionDefName = RandomFactionsMod.GetXenoFactionDefName(randomXenotypeDef, randomFactionDef);
        var factionDef = FindFactionDefByName(xenoFactionDefName);

        if (factionDef != null)
        {
            Messages.Message("RaFa.baseLineReplaced".Translate(randomXenotypeDef.label), MessageTypeDefOf.NeutralEvent);
            return factionDef;
        }

        Log.Warning(
            $"[RandomFactions] Couldn't replace xenotype for faction {randomFactionDef.defName} using xenotype {randomXenotypeDef.defName}");
        return randomFactionDef;
    }

    private XenotypeDef GetRandomNonBaselineXenotypeDef()
    {
        return _violenceCapableNonBaselineXenotypes[_prng.Next(_violenceCapableNonBaselineXenotypes.Count)];
    }

    private static FactionDef FindFactionDefByName(string name)
    {
        return DefDatabase<FactionDef>.AllDefs.FirstOrDefault(def => name.Equals(def.defName));
    }

    private Faction GetRandomFactionWithFilters(Faction[] existingFactions, bool allowDuplicates, params FactionDefFilter[] filters)
    {
        while (true)
        {
            var allFilters = new List<FactionDefFilter>(filters)
            {
                GetDuplicatesFilter(existingFactions, allowDuplicates)
            };

            var filteredDefs = FactionDefFilter.FilterFactionDefs(_definedFactionDefs, allFilters);

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

    private Faction GetRandomNpcFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        return GetRandomFactionWithFilters(existingFactions, allowDuplicates,
            new PlayerFactionDefFilter(false),
            new HiddenFactionDefFilter(false),
            new FactionDefNameFilter(false, _modOffBooksFactionDefNames)
        );
    }

    private Faction GetRandomEnemyFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        return GetRandomFactionWithFilters(existingFactions, allowDuplicates,
            new PlayerFactionDefFilter(false),
            new HiddenFactionDefFilter(false),
            new FactionDefNameFilter(false, _modOffBooksFactionDefNames),
            new PermanentEnemyFactionDefFilter(true)
        );
    }

    private Faction GetRandomRoughFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        return GetRandomFactionWithFilters(existingFactions, allowDuplicates,
            new PlayerFactionDefFilter(false),
            new HiddenFactionDefFilter(false),
            new FactionDefNameFilter(false, _modOffBooksFactionDefNames),
            new PermanentEnemyFactionDefFilter(false),
            new NaturalEnemyFactionDefFilter(true)
        );
    }

    private Faction GetRandomNeutralFaction(Faction[] existingFactions, bool allowDuplicates)
    {
        return GetRandomFactionWithFilters(existingFactions, allowDuplicates,
            new PlayerFactionDefFilter(false),
            new HiddenFactionDefFilter(false),
            new FactionDefNameFilter(false, _modOffBooksFactionDefNames),
            new PermanentEnemyFactionDefFilter(false),
            new NaturalEnemyFactionDefFilter(false)
        );
    }

    private Faction GetRandomNamedFaction(Faction[] existingFactions, bool allowDuplicates, params string[] nameList)
    {
        return GetRandomFactionWithFilters(existingFactions, allowDuplicates,
            new PlayerFactionDefFilter(false),
            new FactionDefNameFilter(false, _modOffBooksFactionDefNames),
            new FactionDefNameFilter(nameList)
        );
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
            Log.Error(
                $"[RandomFactions] Couldn't generate faction with relations from def {def.defName}. Exception: {ex.Message}.");
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
        if (def.permanentEnemy || def.naturalEnemy || def.permanentEnemyToEveryoneExceptPlayer ||
            def.permanentEnemyToEveryoneExcept?.Count > 0 || def.defName == "Insect")
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