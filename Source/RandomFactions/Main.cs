using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RandomFactions.Filters;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RandomFactions;

public class Main : GameComponent
{
    public Main(Game game) { }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        ExecuteFactionReplacement();
    }

    private void ExecuteFactionReplacement()
    {
        Log.Message("[RandomFactions] Applying Random generation rules to factions...");
        FixVfeNewFactionPopups(Find.World);
        Log.Message($"[RandomFactions] Found {DefDatabase<FactionDef>.DefCount} faction definitions: {DefListToString(DefDatabase<FactionDef>.AllDefs)}");

        var hasBiotech = ModsConfig.BiotechActive;
        int xenoPercent;
       
        if (hasBiotech)
        {
            Log.Message($"[RandomFactions] Found {DefDatabase<XenotypeDef>.DefCount} xenotype definitions: {DefListToString(DefDatabase<XenotypeDef>.AllDefs)}");
            xenoPercent = RandomFactionsMod.Settings.xenoPercent;
        }
        else
        {
            xenoPercent = 0;
        }

        var ignoreList = RandomFactionsMod.PatchedXenotypeFactions.Keys.ToArray();

        var factionGenerator = new RandomFactionGenerator(
            xenoPercent,
            DefDatabase<FactionDef>.AllDefs,
            ignoreList.ToArray(),
            hasBiotech,
            RandomFactionsMod.ViolenceCapableNonBaselineXenotypes.Value
        );

        var factionReplacementList = Find.FactionManager.AllFactions.Where(faction =>
            faction.def.categoryTag == RandomFactionsMod.RandomCategoryName && !faction.defeated).ToList();

        foreach (var faction in factionReplacementList)
        {
            if (faction.def.defName.EqualsIgnoreCase("RF_RandomFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenFaction(faction, RandomFactionsMod.Settings.allowDuplicates);
            }
            else if (faction.def.defName.EqualsIgnoreCase("RF_RandomPirateFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenEnemyFaction(faction, RandomFactionsMod.Settings.allowDuplicates);
            }
            else if (faction.def.defName.EqualsIgnoreCase("RF_RandomRoughFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenWarlordFaction(faction, RandomFactionsMod.Settings.allowDuplicates);
            }
            else if (faction.def.defName.EqualsIgnoreCase("RF_RandomTradeFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenTraderFaction(faction, RandomFactionsMod.Settings.allowDuplicates);
            }
            else
            {
                Log.Warning($"[RandomFactions] Faction defName {faction.def.defName} wasn't recognized! Couldn't replace faction {faction.Name} ({faction.def.defName})");
            }
        }

        Log.Message($"[RandomFactions] Random faction generation finished! Replaced {factionReplacementList.Count} factions.");
        MarkUnusedGeneratedFactionsForRemoval();
    }

    private static void MarkUnusedGeneratedFactionsForRemoval()
    {
        var factionsToRemove = Find.FactionManager.AllFactions
            .Where(f =>
                f.def.categoryTag == RandomFactionsMod.RandomCategoryName ||
                (f.def.categoryTag == RandomFactionsMod.XenopatchCategoryName &&
                 f.defeated && f.hidden == true));

        foreach (var faction in factionsToRemove)
        {
            faction.temporary = true;
            Find.FactionManager.Notify_PawnLeftFaction(faction);
        }
    }

    private static void FixVfeNewFactionPopups(World world)
    {
        IEnumerable<FactionDef> factionDefs = RandomFactionsMod.PatchedXenotypeFactions.Values;
        IEnumerable<FactionDef> filterFactionDefs = FactionDefFilter.FilterFactionDefs(
            DefDatabase<FactionDef>.AllDefs, [new CategoryTagFactionDefFilter(RandomFactionsMod.RandomCategoryName)]);

        Type newFactionSpawningStateClassType = null;
        MethodInfo ignoreMethodHandle = null;

        var targetMethod = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Type.EmptyTypes;
                }
            })
            .Where(t => t.Name == "NewFactionSpawningState")
            .SelectMany(t => t.GetMethods().Select(m => new { ClassType = t, Method = m }))
            .FirstOrDefault(x =>
                x.Method.Name == "Ignore" &&
                x.Method.GetParameters().Length == 1 &&
                x.Method.GetParameters()[0].ParameterType.IsAssignableFrom(factionDefs.GetType())
            );

        if (targetMethod != null)
        {
            newFactionSpawningStateClassType = targetMethod.ClassType;
            ignoreMethodHandle = targetMethod.Method;
        }

        if (newFactionSpawningStateClassType == null)
        {
            return;
        }

        object worldComponent = world.GetComponent(newFactionSpawningStateClassType);
        if (worldComponent == null)
        {
            return;
        }

        ignoreMethodHandle.Invoke(worldComponent, [filterFactionDefs]);
        ignoreMethodHandle.Invoke(worldComponent, [factionDefs]);
        Log.Message("[RandomFactions] Invoked World.GetComponent<VFECore.NewFactionSpawningState>().Ignore(...) to tell VFE to ignore random and xenotype-patched faction defs");
    }

    private static string DefListToString(IEnumerable<Def> allDefs)
    {
        return string.Join(", ", allDefs.Select(d => d.defName));
    }
}