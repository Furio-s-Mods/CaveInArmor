using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CaveInArmorMod
{
    public class CaveInArmorModSystem : ModSystem
    {
        private ICoreServerAPI api;
        
        // Dictionary to track active delegate bindings and prevent server memory leaks
        private readonly Dictionary<string, OnDamagedDelegate> activeHandlers = new Dictionary<string, OnDamagedDelegate>();

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.api = api;

            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.PlayerLeave += OnPlayerLeave; // Essential addition to clean up memory
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            if (player.Entity == null) return;

            var healthBehavior = player.Entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBehavior != null)
            {
                // Create a reusable named delegate instance bound to this specific player
                OnDamagedDelegate handler = (float damage, DamageSource damageSource) =>
                {
                    return HandleCaveInDamage(player, damage, damageSource);
                };

                // Store it securely so we can detach it when they log off
                activeHandlers[player.PlayerUID] = handler;
                healthBehavior.onDamaged += handler;
            }
        }

        private void OnPlayerLeave(IServerPlayer player)
        {
            // Safely detach the event handler on logout to eliminate memory leaks
            if (activeHandlers.TryGetValue(player.PlayerUID, out var handler))
            {
                if (player.Entity != null)
                {
                    var healthBehavior = player.Entity.GetBehavior<EntityBehaviorHealth>();
                    if (healthBehavior != null)
                    {
                        healthBehavior.onDamaged -= handler;
                    }
                }
                activeHandlers.Remove(player.PlayerUID);
            }
        }

        private float HandleCaveInDamage(IServerPlayer player, float damage, DamageSource damageSource)
        {
            if (damageSource.Type != EnumDamageType.Crushing || damage <= 0f)
            {
                return damage;
            }

            IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null)
            {
                return damage;
            }

            // Using explicit API Enum types instead of brittle hardcoded index numbers
            double rnd = api.World.Rand.NextDouble();
            ItemSlot armorSlot;

            if ((rnd -= 0.2) < 0.0)
            {
                armorSlot = inv[(int)EnumCharacterDressType.ArmorHead];
            }
            else if (rnd - 0.5 < 0.0)
            {
                armorSlot = inv[(int)EnumCharacterDressType.ArmorBody];
            }
            else
            {
                armorSlot = inv[(int)EnumCharacterDressType.ArmorLegs];
            }

            if (armorSlot == null || armorSlot.Empty)
            {
                return damage;
            }

            IWearableStatsSupplier wearableStats = armorSlot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>();
            if (wearableStats == null || !wearableStats.IsArmorType(armorSlot))
            {
                return damage;
            }

            if (armorSlot.Itemstack.Collectible.GetRemainingDurability(armorSlot.Itemstack) <= 0)
            {
                return damage;
            }

            ProtectionModifiers protMods = wearableStats.GetProtectionModifiers(armorSlot);
            if (protMods == null)
            {
                return damage;
            }

            int weaponTier = damageSource.DamageTier;
            float flatDmgProt = protMods.FlatDamageReduction;
            float percentProt = protMods.RelativeProtection;

            // Run vanilla tier calculations
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

            // Calculate item armor damage
            float durabilityLoss = 0.5f + damage * Math.Max(0.5f, (float)((weaponTier - protMods.ProtectionTier) * 3));
            int durabilityLossInt = GameMath.RoundRandom(api.World.Rand, durabilityLoss);

            armorSlot.Itemstack.Collectible.DamageItem(api.World, player.Entity, armorSlot, durabilityLossInt, true);

            if (armorSlot.Empty)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), player.Entity, null, true, 32f, 1f);
            }

            // Apply modifications to the final damage float
            damage = Math.Max(0f, damage - flatDmgProt);
            damage *= (1f - Math.Max(0f, percentProt));

            armorSlot.MarkDirty();
            return damage;
        }

        public override void Dispose()
        {
            // Catch-all system cleanup during hot-reloads or server shutdowns
            api.Event.PlayerJoin -= OnPlayerJoin;
            api.Event.PlayerLeave -= OnPlayerLeave;
            activeHandlers.Clear();
            
            base.Dispose();
        }
    }
}
