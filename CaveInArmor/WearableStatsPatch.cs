using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CaveInArmor;

[HarmonyPatch(TargetClassName, TargetMethodName)]
public static class WearableStatsPatch
{
    private const string TargetClassName = "Vintagestory.GameContent.ModSystemWearableStats";
    private const string TargetMethodName = "handleDamaged";

    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    public static bool Prefix(IPlayer player, ref float damage, DamageSource dmgSource, ref float __result)
    {
        if (CaveInArmorSystem.Instance is not { 
            Config: var config,  
            CustomLogger: var logger, 
            ServerApi: var sapi, 
        }) return true;

        if (player?.InventoryManager == null || dmgSource == null || config == null || damage <= 0f) return true;
        if (dmgSource.Type != EnumDamageType.Crushing || dmgSource.Source != EnumDamageSource.Block) return true;

        IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (inv == null) return true;

        float initialDamage = damage;

        // logger.Notification($"Processing cave-in hit for {player.PlayerName}. Initial Damage: {initialDamage}. System Mode: {(config.UseLayered ? "Layered" : "Lottery")}, MinimumDamageThreshold={config.MinimumDamageThreshold}");

        if (config.UseLayered && dmgSource.SourcePos != null)
        {
            // 1. Structural Height Metrics
            double playerFeetY = player.Entity.Pos.Y;
            double blockCenterY = dmgSource.SourcePos.Y;
            double deltaY = blockCenterY - playerFeetY;

            int playerBlockY = (int)Math.Floor(playerFeetY);
            int blockBlockY = (int)Math.Floor(blockCenterY);
            
            // CHECK A: Under-Feet Collapse (Soil Instability while climbing)
            if (blockBlockY < playerBlockY || deltaY < 0.0)
            {
                damage = CalcReductionHelper.CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorLegs], damage, dmgSource, initialDamage, config.Horizontal.LayeredLegsMultiplier, "ArmorLegs");
                damage = Math.Max(config.MinimumDamageThreshold, damage);
                
                logger.Notification($"Layered (UNDER_FEET) final modified damage for {player.PlayerName}: {damage}");
                __result = damage; 
                return false; 
            }
            
            bool isVerticalDrop = true;

            // Primary Check: Entity Velocity Vector (The cleanest source of truth)
            if (dmgSource.SourceEntity != null)
            {
                double motionY = dmgSource.SourceEntity.Pos.Motion.Y; // Keep sign to ensure it's downward

                if (motionY < -0.14)
                {
                    isVerticalDrop = true;
                    // logger.Notification($"Trajectory Verified: Pure Vertical Fall (Motion= Y: {motionY:F3})");
                } else
                {
                    isVerticalDrop = false;
                    // logger.Notification($"Trajectory Verified: horizontal Fall (Motion= Y: {motionY:F3})");
                }
            }

            // CHECK B: VERTICAL DROP (Directly overhead onto head/helmet)
            if (isVerticalDrop)
            {
                damage = CalcReductionHelper.CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorHead], damage, dmgSource, initialDamage, config.Vertical.LayeredHeadMultiplier, "ArmorHead");
                damage = CalcReductionHelper.CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorBody], damage, dmgSource, initialDamage, config.Vertical.LayeredTorsoMultiplier, "ArmorBody");
                damage = CalcReductionHelper.CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorLegs], damage, dmgSource, initialDamage, config.Vertical.LayeredLegsMultiplier, "ArmorLegs");   
                damage = Math.Max(config.MinimumDamageThreshold, damage);
                
                logger.Notification($"Layered (VERTICAL) final modified damage for {player.PlayerName}: {damage}");
                __result = damage; 
                return false;
            }
            // CHECK C: HORIZONTAL IMPACT (Landslide, ledge roll, or side swipe)
            else
            {
                damage = CalcReductionHelper.CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorHead], damage, dmgSource, initialDamage, config.Horizontal.LayeredHeadMultiplier, "ArmorHead");
                damage = CalcReductionHelper.CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorBody], damage, dmgSource, initialDamage, config.Horizontal.LayeredTorsoMultiplier, "ArmorBody");
                damage = CalcReductionHelper.CalculateSlotReduction(player, inv[(int)EnumCharacterDressType.ArmorLegs], damage, dmgSource, initialDamage, config.Horizontal.LayeredLegsMultiplier, "ArmorLegs");
                damage = Math.Max(config.MinimumDamageThreshold, damage);
                
                logger.Notification($"Layered (HORIZONTAL) final modified damage for {player.PlayerName}: {damage}");
                __result = damage; 
                return false;
            }
        }
        // lottery mode
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

            damage = CalcReductionHelper.CalculateSlotReduction(player, targetSlot, damage, dmgSource, initialDamage, 1.0f, slotName);
            damage = Math.Max(config.MinimumDamageThreshold, damage);
            
            logger.Notification($"Lottery final modified damage for {player.PlayerName} (Targeted Slot: {slotName}): {damage}");
            __result = damage; 
            return false; 
        }
    }
}
