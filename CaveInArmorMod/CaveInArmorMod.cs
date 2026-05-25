using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CaveInArmorMod
{
    public class CaveInConfig
    {
        public bool Enabled { get; set; }
        public bool EnableDebugLogging { get; set; }
        public bool UseLayered { get; set; }
        public float LayeredHeadMultiplier { get; set; }
        public float LayeredTorsoMultiplier { get; set; }
        public float LayeredLegsMultiplier { get; set; }
        public float DurabilityDamageMultiplier { get; set; }
        public float MinimumDamageThreshold { get; set; }

        public static CaveInConfig CreateDefaultConfig()
        {
            return new CaveInConfig
            {
                Enabled = true,
                EnableDebugLogging = false,
                UseLayered = false,
                LayeredHeadMultiplier = 1.0f,
                LayeredTorsoMultiplier = 0.5f,
                LayeredLegsMultiplier = 0.1f,
                DurabilityDamageMultiplier = 0.1f,
                MinimumDamageThreshold = 0.5f
            };
        }
    }

    public class CaveInArmorModSystem : ModSystem
    {
        private Harmony harmony;
        public const string ModName = "caveinarmor";
        public const string HarmonyId = $"com.furio.{ModName}";
        public const string TargetClassName = "Vintagestory.GameContent.ModSystemWearableStats";
        public const string TargetMethodName = "handleDamaged";

        public static CaveInConfig Config { get; private set; }
        public static ILogger ModLogger { get; private set; }
        public static ICoreServerAPI ServerApi { get; private set; }

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ServerApi = api;
            ModLogger = api.Logger;

            try
            {
                Config = api.LoadModConfig<CaveInConfig>($"{ModName}Config.json");
                if (Config == null)
                {
                    Config = CaveInConfig.CreateDefaultConfig();
                    api.StoreModConfig(Config, $"{ModName}Config.json");
                    ModLogger.Notification($"[{ModName}] Generated fresh fallback configuration file.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[{ModName}] Failed parsing config file. Using default parameters: {ex.Message}");
                Config = CaveInConfig.CreateDefaultConfig();
            }

            if (!Config.Enabled) return;

            harmony = new Harmony(HarmonyId);

            try
            {
                Type targetType = typeof(ModSystemWearableStats);
                MethodInfo originalMethod = AccessTools.Method(targetType, TargetMethodName);

                if (originalMethod == null)
                {
                    ModLogger.Error($"[{ModName}] Critical targeting failure. Target method '{TargetMethodName}' not found!");
                    return;
                }

                MethodInfo prefixMethod = AccessTools.Method(typeof(WearableStatsPatch), nameof(WearableStatsPatch.Prefix));
                harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
                ModLogger.Notification($"[{ModName}] Successfully patched cave-in defense calculations.");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[{ModName}] Failed applying Harmony initialization sequence: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            base.Dispose();
        }
    }

    public static class WearableStatsPatch
    {
        public static bool Prefix(IPlayer player, ref float damage, DamageSource dmgSource, ref float __result)
        {
            if (player?.InventoryManager == null || dmgSource == null || CaveInArmorModSystem.Config == null || damage <= 0f) return true;
            if (dmgSource.Type != EnumDamageType.Crushing) return true;

            IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return true;

            float initialDamage = damage;

            if (CaveInArmorModSystem.Config.EnableDebugLogging)
            {
                CaveInArmorModSystem.ModLogger.Notification($"[{CaveInArmorModSystem.ModName}] Processing cave-in hit for {player.PlayerName}. Initial Damage: {initialDamage}. System Mode: {(CaveInArmorModSystem.Config.UseLayered ? "Layered" : "Lottery")}");
            }

            if (CaveInArmorModSystem.Config.UseLayered)
            {
                damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorHead], damage, dmgSource, initialDamage, CaveInArmorModSystem.Config.LayeredHeadMultiplier, "ArmorHead");
                damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorBody], damage, dmgSource, initialDamage, CaveInArmorModSystem.Config.LayeredTorsoMultiplier, "ArmorBody");
                damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorLegs], damage, dmgSource, initialDamage, CaveInArmorModSystem.Config.LayeredLegsMultiplier, "ArmorLegs");
                
                damage = Math.Max(CaveInArmorModSystem.Config.MinimumDamageThreshold, damage);
                
                if (CaveInArmorModSystem.Config.EnableDebugLogging)
                {
                    CaveInArmorModSystem.ModLogger.Notification($"[{CaveInArmorModSystem.ModName}] Layered final modified damage for {player.PlayerName}: {damage}");
                }
                __result = damage; 
                return false; 
            }
            else
            {
                double rnd = CaveInArmorModSystem.ServerApi.World.Rand.NextDouble();
                ItemSlot targetSlot;
                string slotName;

                if ((rnd -= 0.2) < 0.0)
                {
                    targetSlot = inv[(int)EnumCharacterDressType.ArmorHead];
                    slotName = "ArmorHead";
                }
                else if (rnd - 0.5 < 0.0)
                {
                    targetSlot = inv[(int)EnumCharacterDressType.ArmorBody];
                    slotName = "ArmorBody";
                }
                else
                {
                    targetSlot = inv[(int)EnumCharacterDressType.ArmorLegs];
                    slotName = "ArmorLegs";
                }

                damage = CalculateSlotReduction(player, targetSlot, damage, dmgSource, initialDamage, 1.0f, slotName);
                damage = Math.Max(CaveInArmorModSystem.Config.MinimumDamageThreshold, damage);
                
                if (CaveInArmorModSystem.Config.EnableDebugLogging)
                {
                    CaveInArmorModSystem.ModLogger.Notification($"[{CaveInArmorModSystem.ModName}] Lottery final modified damage for {player.PlayerName} (Targeted Slot: {slotName}): {damage}");
                }
                __result = damage; 
                return false; 
            }
        }

        private static float CalculateSlotReduction(IPlayer player, ItemSlot armorSlot, float currentDamage, DamageSource dmgSource, float initialDamage, float slotMultiplier, string slotDebugName)
        {
            if (currentDamage <= 0f || armorSlot == null || slotMultiplier <= 0f) return currentDamage;
            
            if (armorSlot.Empty)
            {
                if (CaveInArmorModSystem.Config.EnableDebugLogging)
                {
                    CaveInArmorModSystem.ModLogger.Notification($"[{CaveInArmorModSystem.ModName}] Slot [{slotDebugName}] is empty. Skipping mitigation.");
                }
                return currentDamage;
            }
            
            if (armorSlot.Itemstack?.Collectible == null) return currentDamage;

            var wearableStats = armorSlot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>();
            if (wearableStats == null || !wearableStats.IsArmorType(armorSlot)) return currentDamage;
            if (armorSlot.Itemstack.Collectible.GetRemainingDurability(armorSlot.Itemstack) <= 0) return currentDamage;

            ProtectionModifiers protMods = wearableStats.GetProtectionModifiers(armorSlot);
            if (protMods == null) return currentDamage;

            int weaponTier = Math.Max(0, dmgSource.DamageTier);
            float flatDmgProt = protMods.FlatDamageReduction * slotMultiplier;
            float percentProt = protMods.RelativeProtection * slotMultiplier;

            float[] flatLossArray = protMods.PerTierFlatDamageReductionLoss ?? [0f, 0f];
            float[] percLossArray = protMods.PerTierRelativeProtectionLoss ?? [0f, 0f];

            for (int tier = 1; tier <= weaponTier; tier++)
            {
                bool isHigherTier = tier > protMods.ProtectionTier;
                
                int flatIdx = Math.Clamp(isHigherTier ? 1 : 0, 0, flatLossArray.Length - 1);
                int percIdx = Math.Clamp(isHigherTier ? 1 : 0, 0, percLossArray.Length - 1);

                float flatLoss = flatLossArray[flatIdx];
                float percLoss = percLossArray[percIdx];

                if (isHigherTier && protMods.HighDamageTierResistant)
                {
                    flatLoss /= 2f;
                    percLoss /= 2f;
                }

                flatDmgProt -= flatLoss * slotMultiplier;
                percentProt *= 1f - (percLoss * slotMultiplier);
            }

            float durabilityLoss = 0.5f + initialDamage * Math.Max(0.5f, (weaponTier - protMods.ProtectionTier) * 3);
            durabilityLoss *= CaveInArmorModSystem.Config.DurabilityDamageMultiplier;

            int durabilityLossInt = GameMath.RoundRandom(CaveInArmorModSystem.ServerApi.World.Rand, durabilityLoss);

            if (durabilityLossInt > 0)
            {
                armorSlot.Itemstack.Collectible.DamageItem(CaveInArmorModSystem.ServerApi.World, player.Entity, armorSlot, durabilityLossInt, true);
                if (armorSlot.Empty)
                {
                    CaveInArmorModSystem.ServerApi.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), player.Entity, null, true, 32f, 1f);
                }
            }

            float previousDamage = currentDamage;currentDamage = Math.Max(0f, currentDamage - Math.Max(0f, flatDmgProt));
            currentDamage *= 1f - Math.Clamp(percentProt, 0f, 1f);
            if (CaveInArmorModSystem.Config.EnableDebugLogging)
            {
                CaveInArmorModSystem.ModLogger.Notification($"[{CaveInArmorModSystem.ModName}] Slot [{slotDebugName}] reduction process: Incomming={previousDamage} -> Outgoing={currentDamage} | Armor Durability Lost: {durabilityLossInt}");
            }
            armorSlot.MarkDirty();
            return currentDamage;
        }
    }
}
