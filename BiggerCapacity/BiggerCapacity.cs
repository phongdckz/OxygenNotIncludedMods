using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using KMod;
using Newtonsoft.Json;
using UnityEngine;

namespace BiggerCapacity
{
    public class BiggerCapacity : UserMod2
    {
        public static Config config;
        public static string pathToMod;
        public static string pathToConfig;

        public override void OnLoad(Harmony harmony)
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string dllDirectory = Path.GetDirectoryName(dllPath);
            Log($"Mod folder is {dllDirectory}");
            pathToMod = dllDirectory;
            pathToConfig = Path.Combine(dllDirectory, "config.json");
            LoadConfig();
            harmony.PatchAll();
        }

       static Config LoadConfig() {

            if (File.Exists(pathToConfig))
            {
                Log("Loading config.json");
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(pathToConfig));
            }
            else
            {
                Log("Creating config.json");
                config = new Config();

                File.WriteAllText(pathToConfig, JsonConvert.SerializeObject(config, Formatting.Indented));
            }

            return config;
        }

        [HarmonyPatch(typeof(Game), "OnPrefabInit")]
        private static class Game_OnPrefabInit_Patch
        {
            private static void Postfix(Game __instance)
            {
                try
                {
                    Log("Game_OnPrefabInit_Patch");
                    LoadConfig();
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(Storage), "OnSpawn")]
        private static class Storage_OnSpawn_Patch
        {
            private const float MAX_CAPACITY = 99999f;
            private enum CapacityTarget
            {
                Storage,    // Apply to Storage.capacityKg
                Consumer,   // Apply to ConduitConsumer.capacityKG
                FuelTank    // Apply to FuelTank/OxidizerTank/HEPFuelTank
            }

            private class CapacityRule
            {
                // Returns (value, target)
                public Func<Storage, (float value, CapacityTarget target)> Compute;
            }

            // Lookup table
            private static readonly Dictionary<string, CapacityRule> rules = new Dictionary<string, CapacityRule>()
            {
                ["StorageLocker"] = new CapacityRule { 
                    Compute = s =>
                    {
                        float cap = config.capacity.Locker * config.StorageMultiplier;
                        return (cap, CapacityTarget.Storage);
                    }
                },
                ["RationBox"] = new CapacityRule { 
                    Compute = s => 
                    {
                        float cap = config.capacity.RationBox * config.StorageMultiplier;
                        return (cap, CapacityTarget.Storage);
                    }
                },
                ["WoodStorage"] = new CapacityRule 
                { 
                    Compute = s => 
                    {
                        float cap = config.capacity.WoodStorage * config.StorageMultiplier;
                        return (cap, CapacityTarget.Storage);
                    }
                }
            };

            private static void Postfix(Storage __instance)
            {
                Building building = __instance.GetComponent<Building>();

                if (!(bool) (UnityEngine.Object) building)
                {
                    return;
                }
                string prefabId = building.Def.PrefabID;
                float multiplier = config.OtherMultiplier;

                float newCap = __instance.capacityKg * multiplier;
                var target = CapacityTarget.Storage;

                try
                {
                    if (!string.IsNullOrEmpty(prefabId)
                        && rules.TryGetValue(prefabId, out var rule))
                    {
                        (newCap, target) = rule.Compute(__instance);
                    }
                    else if (Mathf.Abs(multiplier - 1f) < 1e-6f)
                    {
                        return;
                    }

                    if (newCap <= 0f)
                    {
                        return;
                    }

                    switch (target)
                    {
                        case CapacityTarget.Storage:
                            ApplyStorage(__instance, newCap);
                            break;
                        case CapacityTarget.FuelTank:
                            ApplyFuelTanks(__instance, newCap);
                            break;
                        case CapacityTarget.Consumer:
                            ApplyConsumer(__instance, newCap);
                            break;
                    }

                    Log($"[{target}] {prefabId} -> {newCap}");
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }

            private static void ApplyStorage(Storage storage, float cap)
            {
                if (storage.TryGetComponent<IUserControlledCapacity>(out var uc))
                {
                    storage.capacityKg = cap;
                    uc.UserMaxCapacity = Mathf.Min(cap, uc.UserMaxCapacity);
                }
                else
                {
                    storage.capacityKg = cap;
                }
            }

            private static void ApplyFuelTanks(Storage s, float cap)
            {
                if (s.TryGetComponent<FuelTank>(out var ft)) ft.physicalFuelCapacity = cap;
                if (s.TryGetComponent<OxidizerTank>(out var ot)) ot.maxFillMass = cap;
                if (s.TryGetComponent<HEPFuelTank>(out var hft)) hft.physicalFuelCapacity = cap;
            }

            private static void ApplyConsumer(Storage s, float cap)
            {
                if (s.TryGetComponent<ConduitConsumer>(out var cc))
                    cc.capacityKG = cap;
            }
        }

        [HarmonyPatch(typeof(Generator), "WattageRating", MethodType.Getter)]
        private static class Generator_WattageRating_Patch
        {
            private static void Postfix(Generator __instance, ref float __result)
            {
                try
                {
                    __result *= config.GeneratorMultiplier;
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(Battery), "OnSpawn")]
        private static class Battery_OnSpawn_Patch
        {
            private static void Postfix(Battery __instance)
            {
                Building building = __instance.GetComponent<Building>();
                if (!(bool) (UnityEngine.Object) building)
                {
                    return;
                }

                string prefabID = building.Def.PrefabID;
                try
                {
                    switch (prefabID)
                    {
                        case "PowerTransformer":
                        case "PowerTransformerSmall":
                            break;
                        default:
                            __instance.capacity *= config.BatteryMultiplier;
                            Log($"{prefabID} -> {__instance.capacity}");
                            break;
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
        }

        [HarmonyPatch(typeof(PowerTransformerConfig), "CreateBuildingDef")]
        private static class PowerTransformerConfig_CreateBuildingDef_Patch
        {
            private static void Postfix(PowerTransformerConfig __instance, BuildingDef __result)
            {
                try
                {
                    __result.GeneratorWattageRating *= config.WireMultiplier;
                    __result.GeneratorBaseCapacity *= config.WireMultiplier;
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
        }


        [HarmonyPatch(typeof(PowerTransformerSmallConfig), "CreateBuildingDef")]
        private static class PowerTransformerSmallConfig_CreateBuildingDef_Patch
        {
            private static void Postfix(PowerTransformerSmallConfig __instance, BuildingDef __result)
            {
                try
                {
                    __result.GeneratorWattageRating *= config.WireMultiplier;
                    __result.GeneratorBaseCapacity *= config.WireMultiplier;
                }
                catch (Exception e)
                {
                        LogException(e);
                }
            }
        }


        [HarmonyPatch(typeof(Wire), "GetMaxWattageAsFloat")]
        private static class Wire_GetMaxWattageAsFloat_Patch
        {
            private static void Postfix(ref float __result)
            {
                try
                {
                    __result *= config.WireMultiplier;
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
        }
        
        // Display battery UI for customized battery capacity
        [HarmonyPatch(typeof(BatteryUI), "Initialize")]
        private static class BatteryUI_Initialize_Patch
        {

            private const float MAX_CAP = 9999999f;
            private static void Postfix(BatteryUI __instance)
            {
                var field = typeof(BatteryUI).GetField("sizeMap", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    return;
                }

                var sizeMap = field.GetValue(__instance) as Dictionary<float, float>;
                if (sizeMap == null)
                {
                    return;
                }

                if (!sizeMap.ContainsKey(MAX_CAP))
                {
                    sizeMap.Add(MAX_CAP, 25f);
                }            
            }
        }

        static void Log(string msg) 
        {
            Debug.Log($"[BiggerCapacity] {msg}");
        }

        static void LogException(Exception e)
        {
            Debug.LogException(e);
        }
    }
}