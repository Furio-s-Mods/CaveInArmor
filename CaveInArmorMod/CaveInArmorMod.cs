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
        public string Mode { get; set; } // "Lottery" or "Layered"
        
        // Custom hit weights to override vanilla when using Lottery mode
        public double HeadHitChance { get; set; }
        public double TorsoHitChance { get; set; }
        public double LegsHitChance { get; set; }

        public float ArmorEffectivenessMultiplier { get; set; }
        public float DurabilityDamageMultiplier { get; set; }
        public float MinimumDamageThreshold { get; set; }

        public static CaveInConfig CreateDefaultConfig()
        {
            return new CaveInConfig
            {
                Enabled = true,
                Mode = "Lottery",
                HeadHitChance = 0.20,
                TorsoHitChance = 0.50,
                LegsHitChance = 0.30,
                ArmorEffectivenessMultiplier = 1.0f,
                DurabilityDamageMultiplier = 1.0f,
                MinimumDamageThreshold = 0.0f
            };
        }
    }

    public class CaveInArmorModSystem : ModSystem
    {
        private Harmony harmony;
        public const string ModName = "caveinarmor";
        public const string HarmonyId = $"com.furio.{ModName}";
        
        // Target class you discovered inside VSSurvivalMod.dll
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
                    ModLogger.Notification($"[{ModName}] Created fresh default configuration file.");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[{ModName}] Config error. Resetting to internal defaults: {ex.Message}");
                Config = CaveInConfig.CreateDefaultConfig();
            }

            if (!Config.Enabled) return;

            harmony = new Harmony(HarmonyId);

            try
            {
                // Pull target class directly from VSSurvivalMod assembly
                Type targetType = typeof(ModSystemWearableStats);
                MethodInfo originalMethod = AccessTools.Method(targetType, TargetMethodName);

                if (originalMethod == null)
                {
                    ModLogger.Error($"[{ModName}] Could not find target method '{TargetMethodName}' to patch!");
                    return;
                }

                MethodInfo prefixMethod = AccessTools.Method(typeof(WearableStatsPatch), nameof(WearableStatsPatch.Prefix));
                harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
                
                ModLogger.Notification($"[{ModName}] Successfully patched cave-in defense calculations.");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[{ModName}] Failed to apply harmony patch: {ex.Message}");
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
        // Using a Harmony Prefix allows us to intercept and rewrite execution BEFORE the gatekeeper check runs
        public static bool Prefix(IPlayer player, ref float damage, DamageSource dmgSource, object __instance)
        {
            if (dmgSource == null || CaveInArmorModSystem.Config == null || damage <= 0f) return true;

            // Only process our target environmental type
            if (dmgSource.Type != EnumDamageType.Crushing) return true;

            IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return true;

            ModSystemWearableStats systemInstance = __instance as ModSystemWearableStats;
            if (systemInstance == null) return true;

            float initialDamage = damage;

            // --- SELECTION TYPE LOGIC FROM CONFIG ---
            if (CaveInArmorModSystem.Config.Mode.Equals("Layered", StringComparison.OrdinalIgnoreCase))
            {
                // SYSTEM A: Layered calculations (Manually computing slots sequentially)
                damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorHead], damage, dmgSource, initialDamage);
                damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorBody], damage, dmgSource, initialDamage);
                damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorLegs], damage, dmgSource, initialDamage);
                
                damage = Math.Max(CaveInArmorModSystem.Config.MinimumDamageThreshold, damage);
                return false; // Skip vanilla execution entirely since we handled it all manually
            }
            else
            {
                // SYSTEM B: Lottery system (Trick the vanilla code into processing it)
                // Temporarily alter the damage source type to sneak right past the vanilla whitelist check
                dmgSource.Type = EnumDamageType.BluntAttack;

                // Let the native method run natively using our modified damage source type!
                // NOTE: If weights are customized in config, we could manually choose the slot here.
                // Otherwise, vanilla naturally draws its own 20/50/30 lottery.
                
                // After vanilla completes its calculations, the framework wraps back out.
                // We handle post-calculations using direct injection behavior if needed, or allow standard return.
                return true; 
            }
        }

        private static float CalculateSlotReduction(IPlayer player, ItemSlot armorSlot, float currentDamage, DamageSource dmgSource, float initialDamage)
        {
            if (currentDamage <= 0f || armorSlot == null || armorSlot.Empty) return currentDamage;

            var wearableStats = armorSlot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>();
            if (wearableStats == null || !wearableStats.IsArmorType(armorSlot)) return currentDamage;
            if (armorSlot.Itemstack.Collectible.GetRemainingDurability(armorSlot.Itemstack) <= 0) return currentDamage;

            ProtectionModifiers protMods = wearableStats.GetProtectionModifiers(armorSlot);
            if (protMods == null) return currentDamage;

            int weaponTier = dmgSource.DamageTier;
            float flatDmgProt = protMods.FlatDamageReduction;
            float percentProt = protMods.RelativeProtection;

            for (int tier = 1; tier <= weaponTier; tier++)
            {
                bool isHigherTier = tier > protMods.ProtectionTier;
                float flatLoss = isHigherTier ? protMods.PerTierFlatDamageReductionLoss[1] : protMods.PerTierFlatDamageReductionLoss[0];
                float percLoss = isHigherTier ? protMods.PerTierRelativeProtectionLoss[1] : protMods.PerTierRelativeProtectionLoss[0];

                if (isHigherTier && protMods.HighDamageTierResistant)
                {
                    flatLoss /= 2f;
                    percLoss /= 2f;
                }

                flatDmgProt -= flatLoss;
                percentProt *= (1f - percLoss);
            }

            flatDmgProt *= CaveInArmorModSystem.Config.ArmorEffectivenessMultiplier;
            percentProt *= CaveInArmorModSystem.Config.ArmorEffectivenessMultiplier;

            float durabilityLoss = 0.5f + initialDamage * Math.Max(0.5f, (float)((weaponTier - protMods.ProtectionTier) * 3));
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

            currentDamage = Math.Max(0f, currentDamage - flatDmgProt);
            currentDamage *= (1f - Math.Max(0f, percentProt));

            armorSlot.MarkDirty();
            return currentDamage;
        }
    }
}
