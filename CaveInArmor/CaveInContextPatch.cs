using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace CaveInArmor;

[HarmonyPatch]
public static class CaveInContextPatch
{
    // Thread Safety: Separate instance tracking per thread
    [ThreadStatic]
    private static EntityBlockFalling _activeFallingBlock;

    [HarmonyPatch(typeof(EntityBlockFalling), "OnGameTick")]
    [HarmonyPrefix]
    public static void OnGameTick_Prefix(EntityBlockFalling __instance)
    {
        _activeFallingBlock = __instance;
    }

    // Exception Safety: A Finalizer ALWAYS runs, even if OnGameTick throws an error.
    // This prevents stale state leakage if a cave-in crashes the game logic.
    [HarmonyPatch(typeof(EntityBlockFalling), "OnGameTick")]
    [HarmonyFinalizer]
    public static void OnGameTick_Finalizer()
    {
        _activeFallingBlock = null;
    }

    [HarmonyPatch(typeof(Entity), nameof(Entity.ReceiveDamage))]
    [HarmonyPrefix]
    public static void ReceiveDamage_Prefix(DamageSource damageSource)
    {
        // Synchronous Catch: WalkEntities runs immediately, so this flag is guaranteed active.
        if (_activeFallingBlock != null && damageSource != null)
        {
            if (damageSource.Source == EnumDamageSource.Block && damageSource.Type == EnumDamageType.Crushing)
            {
                // Inject the live entity into the reference type
                damageSource.SourceEntity = _activeFallingBlock;
                
                // Live debugging check:
                // _activeFallingBlock.World.Logger.Warning($"[CaveInArmor] Injected falling block! Motion Y: {_activeFallingBlock.Pos.Motion}");
            }
        }
    }
}