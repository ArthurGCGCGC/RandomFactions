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
using System.Reflection;
using HugsLib;
using HugsLib.Settings;
using RandomFactions.filters;
using RimWorld;
using RimWorld.Planet;
using UnityEngine.SceneManagement;
using Verse;

namespace RandomFactions;

public class RandomFactionsMod : ModBase
{
    public const string RandomCategoryName = "Random";
    private const string XenopatchCategoryName = "Xenopatch";

    private static readonly HashSet<string> ignoredFactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Insect", //Usually wouldn't be required, but it's here for compatibility with 'Vanilla Factions Expanded - Insectoids 2', which unhides this faction
        "Empire", //Royalty
        "Salvagers", //Odyssey
        "TradersGuild" //Odyssey
    };

    private readonly Dictionary<string, FactionDef> patchedXenotypeFactions = new();

    private readonly Dictionary<FactionDef, int> randCountRecord = new();

    private readonly Lazy<List<XenotypeDef>> violenceCapableNonBaselineXenotypes = new(() =>
        GetViolenceCapableNonBaselineXenotypes()
            .ToList());

    private readonly Dictionary<FactionDef, int> zeroCountRecord = new();
    private SettingHandle<bool> allowDuplicates;
    private SettingHandle<bool> removeOtherFactions;
    private SettingHandle<int> xenoPercentHandle;

    public RandomFactionsMod()
    {
        // constructor (invoked by reflection, do not add parameters)
        Logger.Trace("RandomFactions constructed");
    }

    public override string ModIdentifier =>
        /*
Each ModBase class needs to have a unique identifier. Provide yours by overriding the ModIdentifier property. The identifier will be used in the settings XML to store your settings, so avoid spaces and special characters. You will get an exception if you provide an improper identifier.
         */
        "RandFactions";

    public static bool IsFactionXenotypePatchable(FactionDef def)
    {
        //The baselinerChance check is to prevent the replacement of non-baseliner xenotype factions
        return !(def.isPlayer || def.hidden || def.maxConfigurableAtWorldCreation <= 1
                 || RandomCategoryName.EqualsIgnoreCase(def.categoryTag) || def.BaselinerChance < 1);
    }

    private static string DefListToString(IEnumerable<Def> allDefs)
    {
        return string.Join(", ", allDefs.Select(d => d.defName));
    }

    public static string GetXenoFactionDefName(XenotypeDef xdef, FactionDef fdef)
    {
        return $"{xdef.defName}{fdef.defName}";
    }
    /*
Property Notes: HugsLib.ModBase.*

.Logger

The Logger property allows a mod to write identifiable messages to the console. Error and Warning methods are also available. Calling:
Logger.Message("test");
will result in the following console output:
[ModIdentifier] test
Additionally, the Trace method of the logger will write a console message only if Rimworld is in Dev mode.

.ModIsActive

Returns true if the mod is enabled in the Mods dialog. Disabled mods would not be loaded or instantiated, but if a mod was enabled, and then disabled in the Mods dialog this property will return false.
This property is no longer useful as of A17, since the game restarts when the mod configuration changes.

.Settings

Returns the ModSettingsPack for your mod, from where you can get your SettingsHandles. See the wiki page of creating configurable settings for more information.

.ModContentPack

Returns the ModContentPack for your mod. This can be used to access the name and PackageId, as well as loading custom files from your mod's directory.

.HarmonyInstance

All assemblies that declare a class that extends ModBase are automatically assigned a HarmonyInstance and their Harmony patches are applied. This is where the Harmony instance for each ModBase instance is stored.

.HarmonyAutoPatch

Override this and return false if you don't want a HarmonyInstance to be automatically created and the patches in your assembly applied. Having multiple ModBase classes in your assembly will produce warnings if their HarmonyAutoPatch is not disabled, but your assembly will only be patched once.
*/

    //        public override void EarlyInitialize()
    //        {
    //            /*
    //Called during Verse.Mod instantiation, and only if your class has the [EarlyInit] attribute.
    //Nothing is yet loaded at this point, so you might want to place your initialization code in Initialize, instead this method is mostly used for custom patching.
    //You will not receive any callbacks on Update, FixedUpdate, OnGUI and SettingsChanged until after the Initialize callback comes through.
    //Initialize will still be called at the normal time, regardless of the [EarlyInit] attribute.*/
    //            base.EarlyInitialize();
    //        }

    public override void DefsLoaded()
    {
        /*
Called after all Defs are loaded.
This happens when game loading has completed, after Initialize is called. This is a good time to inject any Random defs. Make sure you call HugsLib.InjectedDefHasher.GiveShortHasToDef on any defs you manually instantiate to avoid def collisions (it's a vanilla thing).
Since A17 it no longer matters where you initialize your settings handles, since the game automatically restarts both when the mod configuration or the language changes. This means that both Initialize and DefsLoaded are only ever called once per ModBase instance.*/
        base.DefsLoaded();

        // add mod options
        removeOtherFactions = Settings.GetHandle(
            "removeOtherFactions",
            "RaFa.reorganiseFactions".Translate(),
            "RaFa.reorganiseFactionsTT".Translate(),
            true);

        if (removeOtherFactions.Value)
        {
            ZeroCountFactionDefs();
        }

        xenoPercentHandle = Settings.GetHandle(
            "PercentXenotype",
            "RaFa.xenotypePercent".Translate(),
            "RaFa.xenotypePercentTT".Translate(),
            40,
            Validators.IntRangeValidator(0, 100));
        //xenoPercentHandle.ValueChanged += handle => {
        //    Logger.Message("Xenotype changed to " + xenoPercentHandle.Value);
        //};

        // add procedural defs
        // if Biotech DLC is installed, patch-in xeno versions of human factions
        if (ModsConfig.BiotechActive)
        {
            CreateXenoFactions();
            Logger.Message("Created Xenotype versions of Baseliner factions.");
            // if VFE mods are installed, need to tell VFE Core to update the cache or there will be a Dictionary key error
            // call VFECore.ScenPartUtility.SetCache() by reflection (which is tricky because it is in a different assdembly)
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

                    //Logger.Message(string.Format("Found {0}.{1}", asmb.FullName, classType.Name));
                    var methodHandle = classType.GetMethod("SetCache");
                    if (methodHandle == null)
                    {
                        continue;
                    }

                    methodHandle.Invoke(null, null);
                    Logger.Message(
                        "Invoked VFECore.ScenPartUtility.SetCache() to refresh VFE internal cache");
                    done = true;
                }
            }
            // if VFE is installed, then we also need to invoke Find.World.GetComponent<NewFactionSpawningState>().Ignore(factionDef); 
            // to keep it from pestering the player about 100+ new factions being added to the game
        }

        // allow or disallow picking factions already added:
        allowDuplicates = Settings.GetHandle<bool>(
            "allowDuplicates",
            "RaFa.allowDuplicates".Translate(),
            "RaFa.allowDuplicatesTT".Translate());
    }

    private void CreateXenoFactions()
    {
        var newDefs = new List<FactionDef>();

        foreach (var def in DefDatabase<FactionDef>.AllDefs)
        {
            if (!IsFactionXenotypePatchable(def))
            {
                continue;
            }

            foreach (var xenotypeDef in violenceCapableNonBaselineXenotypes.Value)
            {
                var defCopy = CloneDef(def);
                defCopy.defName = GetXenoFactionDefName(xenotypeDef, defCopy);
                defCopy.categoryTag = XenopatchCategoryName;
                defCopy.label = $"{xenotypeDef.label} {defCopy.label}";
                var xenoChance = new XenotypeChance(xenotypeDef, 1.0f);
                var xenotypeChances = new List<XenotypeChance>
                {
                    xenoChance
                };
                var newXenoSet = new XenotypeSet();
                // I think Ludeon Studios hates procedural generation. Why make XenotypeSet read-only with no constructor?!
                // Need to use reflection voodoo to modify private variable (whose name might change in a future version)
                var fields =
                    typeof(XenotypeSet).GetFields(BindingFlags.NonPublic | BindingFlags.Public |
                                                  BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.FieldType.IsAssignableFrom(xenotypeChances.GetType()))
                    {
                        field.SetValue(newXenoSet, xenotypeChances);
                    }
                }

                defCopy.xenotypeSet = newXenoSet;
                defCopy.maxConfigurableAtWorldCreation = 0; // NOTE:Faction Control messes with this number
                defCopy.hidden = true; // will unhide at game load time
                newDefs.Add(defCopy);
                //Logger.Trace(string.Format("Generated procedural faction def {0} has xenotype set: {1}", defCopy.defName, XenotypeSetToString(defCopy.xenotypeSet)));
            }
        }

        foreach (var def in newDefs)
        {
            patchedXenotypeFactions.Add(def.defName, def);
            DefDatabase<FactionDef>.Add(def);
        }
    }

    private static List<XenotypeDef> GetViolenceCapableNonBaselineXenotypes()
    {
        return DefDatabase<XenotypeDef>.AllDefs.Where(x =>
        {
            //To prevent replacing baseliners with baseliners
            if (x == XenotypeDefOf.Baseliner)
            {
                return false;
            }

            if (x.genes == null)
            {
                return true;
            }

            var combinedDisabled = WorkTags.None;
            foreach (var gene in x.genes)
            {
                combinedDisabled |= gene.disabledWorkTags;
            }

            // Keep only xenotypes that do NOT disable violent work, otherwise their generation will throw an exception since you can't have faction leaders incapable of violence!
            return (combinedDisabled & WorkTags.Violent) == 0;
        }).ToList();
    }

    private static FactionDef CloneDef(FactionDef def)
    {
        // use reflection magic to do a 1-deep clone of the def
        var cpy = new FactionDef();
        ReflectionCopy(def, cpy);
        cpy.debugRandomId = (ushort)(def.debugRandomId + 1);
        return cpy;
    }

    private static void ReflectionCopy(object a, object b)
    {
        var fields = a.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var value = field.GetValue(a);
            field.SetValue(b, value);
        }
        /*
        PropertyInfo[] props = A.GetType().GetProperties();
        foreach(PropertyInfo prop in props)
        {
            var value = prop.GetValue(A, null);
            prop.SetValue(B, value, null);
        }*/
    }

    public override void SettingsChanged()
    {
        /*
Called after the player closes the Mod Settings dialog after changing any setting.
Note, that the setting changed may belong to another mod.*/
        base.SettingsChanged();
        if (removeOtherFactions.Value)
        {
            ZeroCountFactionDefs();
        }
        else
        {
            UndoZeroCountFactionDefs();
        }
    }

    private void ZeroCountFactionDefs()
    {
        foreach (var def in DefDatabase<FactionDef>.AllDefs)
        {
            if (!RandomCategoryName.EqualsIgnoreCase(def.categoryTag) && def.configurationListOrderPriority < 5)
            {
                def.configurationListOrderPriority = Math.Max(def.configurationListOrderPriority + 5, 5);
            }

            if (def.hidden || def.isPlayer || RandomCategoryName.EqualsIgnoreCase(def.categoryTag)
                || ignoredFactions.Contains(def.defName)
               )
            {
                continue;
            }

            zeroCountRecord[def] = def.startingCountAtWorldCreation; // save for later undo operation
            def.startingCountAtWorldCreation = 0;
        }

        foreach (var def in randCountRecord.Keys)
        {
            var val = randCountRecord[def];
            def.startingCountAtWorldCreation = val;
        }
    }

    private void UndoZeroCountFactionDefs()
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

    public override void Update()
    {
        /*
Called on each frame.
Keep in mind that frame rate varies significantly, so this callback is recommended only to do any custom drawing.*/
    }

    public override void SceneLoaded(Scene scene)
    {
        /*
Called after a Unity scene change. Receives a UnityEngine.SceneManagement.Scene type argument.
There are two scenes in Rimworld- Entry and Play, which stand for the menu, and the game itself. Use Verse.GenScene to check which scene has been loaded.
Note, that not everything may be initialized after the scene change, and the game may be in the middle of map loading or generation.*/
        base.SceneLoaded(scene);
        HideXenoPatches(GenScene.InEntryScene);
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

    public override void WorldLoaded()
    {
        /*
Called after the game has started and the world has been initialized.
Any maps may not have been initialized at this point.
This is a good place to get your UtilityWorldObjects with the data you store in the save file. See the appropriate wiki page on how to use those.
This is only called after the game has started, not on the "select landing spot" world map.
*/
        base.WorldLoaded();
        Logger.Message("World loaded! Applying Random generation rules to factions...");
        FixVfeNewFactionPopups(Find.World);
        Logger.Trace(
            $"Found {DefDatabase<FactionDef>.DefCount} faction definitions: {DefListToString(DefDatabase<FactionDef>.AllDefs)}");
        var hasBiotech = ModsConfig.BiotechActive;
        if (hasBiotech)
        {
            Logger.Trace(
                $"Found {DefDatabase<XenotypeDef>.DefCount} xenotype definitions: {DefListToString(DefDatabase<XenotypeDef>.AllDefs)}");
        }

        // load save data store (if it exists)
        //var dataSore = Find.World.GetComponent<RandFacDataStore>();
        // ignore generated factions when choosing random factions
        var ignoreList = patchedXenotypeFactions.Keys.ToArray();

        var xenoPercent = xenoPercentHandle.Value;
        if (!hasBiotech)
        {
            xenoPercent = 0;
        }

        var factionGenerator = new RandomFactionGenerator(xenoPercent, DefDatabase<FactionDef>.AllDefs,
            ignoreList.ToArray(),
            hasBiotech, violenceCapableNonBaselineXenotypes.Value, Logger);

        var factionReplacementList = Find.FactionManager.AllFactions.Where(faction =>
            faction.def.categoryTag.EqualsIgnoreCase(RandomCategoryName) && !faction.defeated).ToList();

        foreach (var faction in factionReplacementList)
        {
            if (faction.def.defName.EqualsIgnoreCase("RF_RandomFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenFaction(faction, allowDuplicates.Value);
            }
            else if (faction.def.defName.EqualsIgnoreCase("RF_RandomPirateFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenEnemyFaction(faction, allowDuplicates.Value);
            }
            else if (faction.def.defName.EqualsIgnoreCase("RF_RandomRoughFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenWarlordFaction(faction, allowDuplicates.Value);
            }
            else if (faction.def.defName.EqualsIgnoreCase("RF_RandomTradeFaction"))
            {
                factionGenerator.ReplaceWithRandomNonHiddenTraderFaction(faction, allowDuplicates.Value);
            }
            else
            {
                Logger.Warning(
                    $"Faction defName {faction.def.defName} wasn't recognized! Couldn't replace faction {faction.Name} ({faction.def.defName})");
            }
        }

        Logger.Message($"...Random faction generation complete! Replaced {factionReplacementList.Count} factions.");
    }


    private void FixVfeNewFactionPopups(World world)
    {
        IEnumerable<FactionDef> factionDefs = patchedXenotypeFactions.Values;
        IEnumerable<FactionDef> filterFactionDefs = FactionDefFilter.FilterFactionDefs(
            DefDatabase<FactionDef>.AllDefs, new CategoryTagFactionDefFilter(RandomCategoryName));
        // invoke Find.World.GetComponent<VFECore.NewFactionSpawningState>().Ignore(factionDef) 
        // for all off-books factions or the player will be buried in pop-up spam
        Type newFactionSpawningStateClassType = null;
        MethodInfo ignoreMethodHandle = null;
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

                if (!"NewFactionSpawningState".Equals(classType.Name))
                {
                    continue;
                }

                //Logger.Message(string.Format("Found {0}.{1}", asmb.FullName, classType.Name));
                var methodHandles =
                    classType
                        .GetMethods(); // calling classType.GetMethod("Ignore") throws ambiguous match exception
                // looking for VFECore.NewFactionSpawningState.Ignore(IEnumerable<FactionDef> factions)
                foreach (var methodHandle in methodHandles)
                {
                    var methParams = methodHandle.GetParameters();
                    //string t = "";
                    //foreach (var param in methParams) { t += " " + param.ParameterType.Name; }
                    //Logger.Message(string.Format("Found {0}.{1}.{2}({3})", asmb.FullName, classType.Name, methodHandle.Name, t));
                    if (!"Ignore".Equals(methodHandle.Name)
                        || methParams.Length != 1
                        || !methParams[0].ParameterType.IsAssignableFrom(factionDefs.GetType()))
                    {
                        continue;
                    }

                    newFactionSpawningStateClassType = classType;
                    ignoreMethodHandle = methodHandle;
                    done = true;
                    break;
                }
            }
        }

        if (newFactionSpawningStateClassType == null)
        {
            return;
        }

        // VFE installed and WorldComponent VFECore.NewFactionSpawningState found
        // tell VFE to ignore patch factions
        object worldComponent = world.GetComponent(newFactionSpawningStateClassType);
        if (worldComponent == null)
        {
            return;
        }

        ignoreMethodHandle.Invoke(worldComponent, [filterFactionDefs]);
        ignoreMethodHandle.Invoke(worldComponent, [factionDefs]);
        Logger.Message(
            "Invoked World.GetComponent<VFECore.NewFactionSpawningState>().Ignore(...) to tell VFE to ignore random and xenotype-patched faction defs");
    }
}