using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CaveInArmor;

public static class WearableStatsPatch
{
    public static bool Prefix(IPlayer player, ref float damage, DamageSource dmgSource, ref float __result)
    {
        if (CaveInArmorSystem.Instance is not { 
            Config: var config,  
            CustomLogger: var logger, 
            ServerApi: var sapi, 
        }) return true;

        if (player?.InventoryManager == null || dmgSource == null || config == null || damage <= 0f) return true;
        if (dmgSource.Type != EnumDamageType.Crushing) return true;

        IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (inv == null) return true;

        float initialDamage = damage;

        logger.Notification($"Processing cave-in hit for {player.PlayerName}. Initial Damage: {initialDamage}. System Mode: {(config.UseLayered ? "Layered" : "Lottery")}, MinimumDamageThreshold={config.MinimumDamageThreshold}");

        if (config.UseLayered)
        {
            damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorHead], damage, dmgSource, initialDamage, config.LayeredHeadMultiplier, "ArmorHead");
            damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorBody], damage, dmgSource, initialDamage, config.LayeredTorsoMultiplier, "ArmorBody");
            damage = CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorLegs], damage, dmgSource, initialDamage, config.LayeredLegsMultiplier, "ArmorLegs");
            
            damage = Math.Max(config.MinimumDamageThreshold, damage);
            
            logger.Notification($"Layered final modified damage for {player.PlayerName}: {damage}");
            __result = damage; 
            return false; 
        }
        else
        {
            double rnd = sapi.World.Rand.NextDouble();
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
            damage = Math.Max(config.MinimumDamageThreshold, damage);
            
            logger.Notification($"Lottery final modified damage for {player.PlayerName} (Targeted Slot: {slotName}): {damage}");
            __result = damage; 
            return false; 
        }
    }

    private static float CalculateSlotReduction(IPlayer player, ItemSlot armorSlot, float currentDamage, DamageSource dmgSource, float initialDamage, float slotMultiplier, string slotDebugName)
    {
        if (currentDamage <= 0f || armorSlot == null || slotMultiplier <= 0f) return currentDamage;

        if (CaveInArmorSystem.Instance is not { 
            Config: var config, 
            CustomLogger: var logger, 
            ServerApi: var sapi, 
        }) return currentDamage;
        
        if (armorSlot.Empty)
        {
            logger.Notification($"Slot [{slotDebugName}] is empty. Skipping mitigation.");
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
        durabilityLoss *= config.DurabilityDamageMultiplier;

        int durabilityLossInt = GameMath.RoundRandom(sapi.World.Rand, durabilityLoss);

        if (durabilityLossInt > 0)
        {
            armorSlot.Itemstack.Collectible.DamageItem(sapi.World, player.Entity, armorSlot, durabilityLossInt, true);
            if (armorSlot.Empty)
            {
                sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), player.Entity, null, true, 32f, 1f);
            }
        }

        float previousDamage = currentDamage;currentDamage = Math.Max(0f, currentDamage - Math.Max(0f, flatDmgProt));
        currentDamage *= 1f - Math.Clamp(percentProt, 0f, 1f);
        armorSlot.MarkDirty();
        
        logger.Notification($"Slot [{slotDebugName}] reduction process: Incomming={previousDamage} -> Outgoing={currentDamage} | Armor Durability Lost: {durabilityLossInt}");
        return currentDamage;
    }
}
