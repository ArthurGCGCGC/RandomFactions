/*
# Random Factions Rimworld Mod
Author: Dr. Plantabyte (aka Christopher C. Hall)
## CC BY 4.0
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;

namespace RandomFactions;

[StaticConstructorOnStartup]
public class RandomFactionsMod : Mod
{
    public const string RandomCategoryName = "Random";
    internal const string XenopatchCategoryName = "Xenopatch";

    private static readonly HashSet<string> ignoredFactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Insect",
        "Empire",
        "Salvagers",
        "TradersGuild"
    };

    private static readonly HashSet<string> ignoredXenotypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AG_RandomCustom",
        "PSRX_RandomXenotype"
    };

    internal static readonly Dictionary<string, FactionDef> PatchedXenotypeFactions = new();
    private static readonly Dictionary<FactionDef, int> randCountRecord = new();
    private static readonly Dictionary<FactionDef, int> zeroCountRecord = new();

    internal static readonly Lazy<List<XenotypeDef>> ViolenceCapableNonBaselineXenotypes =
        new(GetViolenceCapableNonBaselineXenotypes);

    static RandomFactionsMod()
    {
        LongEventHandler.QueueLongEvent(InitializeDefs, "RandomFactions:InitializeDefs", false, null);
    }

    public RandomFactionsMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<Settings>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static Settings Settings { get; private set; }

    private static void InitializeDefs()
    {
        if (Settings.removeOtherFactions)
        {
            ZeroCountFactionDefs();
        }

        if (ModsConfig.BiotechActive)
        {
            CreateXenoFactions();
            Log.Message("[RandomFactions] Created Xenotype versions of Baseliner factions.");

            var done = false;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (done)
                {
                    break;
                }

                foreach (var classType in assembly.GetTypes())
                {
                    if (done)
                    {
                        break;
                    }

                    if (!"ScenPartUtility".Equals(classType.Name))
                    {
                        continue;
                    }

                    var methodHandle = classType.GetMethod("SetCache");
                    if (methodHandle == null)
                    {
                        continue;
                    }

                    methodHandle.Invoke(null, null);
                    Log.Message("[RandomFactions] Invoked VFECore.ScenPartUtility.SetCache() to refresh VFE internal cache");
                    done = true;
                }
            }
        }
    }

    public override string SettingsCategory()
    {
        return "Random Factions";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        var prevRemove = Settings.removeOtherFactions;
        listing.CheckboxLabeled("RaFa.reorganiseFactions".Translate(), ref Settings.removeOtherFactions, "RaFa.reorganiseFactionsTT".Translate());
        if (prevRemove != Settings.removeOtherFactions)
        {
            if (Settings.removeOtherFactions)
            {
                ZeroCountFactionDefs();
            }
            else
            {
                UndoZeroCountFactionDefs();
            }
        }

        listing.Label($"{"RaFa.xenotypePercent".Translate()}: {Settings.xenoPercent}%");
        Settings.xenoPercent = (int)listing.Slider(Settings.xenoPercent, 0, 100);

        listing.CheckboxLabeled("RaFa.allowDuplicates".Translate(), ref Settings.allowDuplicates, "RaFa.allowDuplicatesTT".Translate());

        listing.End();
        base.DoSettingsWindowContents(inRect);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideXenoPatches(GenScene.InEntryScene);
    }

    public static bool IsFactionXenotypePatchable(FactionDef def)
    {
        return !(def.isPlayer || def.hidden || def.maxConfigurableAtWorldCreation <= 1
                 || RandomCategoryName.EqualsIgnoreCase(def.categoryTag) || def.BaselinerChance < 1);
    }

    public static string GetXenoFactionDefName(XenotypeDef xdef, FactionDef fdef)
    {
        return $"{xdef.defName}{fdef.defName}";
    }

    private static void CreateXenoFactions()
    {
        var newDefs = new List<FactionDef>();

        foreach (var def in DefDatabase<FactionDef>.AllDefs)
        {
            if (!IsFactionXenotypePatchable(def))
            {
                continue;
            }

            foreach (var xenotypeDef in ViolenceCapableNonBaselineXenotypes.Value)
            {
                var defCopy = CloneDef(def);
                defCopy.defName = GetXenoFactionDefName(xenotypeDef, defCopy);
                defCopy.categoryTag = XenopatchCategoryName;
                defCopy.label = $"{xenotypeDef.label} {defCopy.label}";
                var xenoChance = new XenotypeChance(xenotypeDef, 1.0f);
                var xenotypeChances = new List<XenotypeChance> { xenoChance };
                var newXenoSet = new XenotypeSet();

                var fields = typeof(XenotypeSet).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.FieldType.IsAssignableFrom(xenotypeChances.GetType()))
                    {
                        field.SetValue(newXenoSet, xenotypeChances);
                    }
                }

                defCopy.xenotypeSet = newXenoSet;
                defCopy.maxConfigurableAtWorldCreation = 0;
                defCopy.hidden = true;
                newDefs.Add(defCopy);
            }
        }

        foreach (var def in newDefs)
        {
            PatchedXenotypeFactions.Add(def.defName, def);
            DefDatabase<FactionDef>.Add(def);
        }
    }

    private static List<XenotypeDef> GetViolenceCapableNonBaselineXenotypes()
    {
        return DefDatabase<XenotypeDef>.AllDefs.Where(x =>
        {
            if (ignoredXenotypes.Contains(x.defName))
            {
                return false;
            }

            //To prevent replacing baseliners with baseliners
            if (x == XenotypeDefOf.Baseliner)
            {
                return false;
            }

            // Keep only xenotypes that do NOT disable violent work, otherwise their generation will throw an exception since you can't have faction leaders incapable of violence!
            return x.genes == null || x.genes.All(gene => !gene.disabledWorkTags.HasFlag(WorkTags.Violent));
        }).ToList();
    }

    private static FactionDef CloneDef(FactionDef def)
    {
        // use reflection magic to do a 1-deep clone of the def
        var factionCopy = new FactionDef();
        ReflectionCopy(def, factionCopy);
        factionCopy.debugRandomId = (ushort)(def.debugRandomId + 1);
        return factionCopy;
    }

    private static void ReflectionCopy(object a, object b)
    {
        var fields = a.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var value = field.GetValue(a);
            field.SetValue(b, value);
        }
    }

    private static void ZeroCountFactionDefs()
    {
        foreach (var def in DefDatabase<FactionDef>.AllDefs)
        {
            if (!RandomCategoryName.EqualsIgnoreCase(def.categoryTag) && def.configurationListOrderPriority < 5)
            {
                def.configurationListOrderPriority = Math.Max(def.configurationListOrderPriority + 5, 5);
            }

            if (def.hidden || def.isPlayer || RandomCategoryName.EqualsIgnoreCase(def.categoryTag)
                || ignoredFactions.Contains(def.defName))
            {
                continue;
            }

            zeroCountRecord[def] = def.startingCountAtWorldCreation;
            def.startingCountAtWorldCreation = 0;
        }

        foreach (var def in randCountRecord.Keys)
        {
            var val = randCountRecord[def];
            def.startingCountAtWorldCreation = val;
        }
    }

    private static void UndoZeroCountFactionDefs()
    {
        foreach (var def in zeroCountRecord.Keys)
        {
            var val = zeroCountRecord[def];
            def.startingCountAtWorldCreation = val;
        }

        foreach (var def in DefDatabase<FactionDef>.AllDefs.Where(x =>
                     RandomCategoryName.EqualsIgnoreCase(x.categoryTag)))
        {
            randCountRecord[def] = def.startingCountAtWorldCreation;
            def.startingCountAtWorldCreation = 0;
        }
    }

    private static void HideXenoPatches(bool hide)
    {
        foreach (var def in DefDatabase<FactionDef>.AllDefs)
        {
            if (XenopatchCategoryName.EqualsIgnoreCase(def.categoryTag))
            {
                def.hidden = hide;
            }
        }
    }
}