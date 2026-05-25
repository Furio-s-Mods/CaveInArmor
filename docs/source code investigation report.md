## 1. Damage Generation (EntityBlockFalling.OnGameTick)
When a ceiling block collapses, it becomes an EntityBlockFalling entity. During its game tick, it scans for entities in its way and generates the crushing damage source using this exact block of code:

```cs
bool nowhit = entity.ReceiveDamage(new DamageSource
{
    Source = EnumDamageSource.Block,
    Type = EnumDamageType.Crushing,
    SourceBlock = this.Block,
    SourcePos = this.Pos.XYZ
}, 10f * (float)Math.Abs(this.Pos.Motion.Y) * this.impactDamageMul);
```

* The Formula: $\text{Damage} = 10 \times |\text{Fall Velocity}| \times \text{Impact Multiplier}$
* The Problem: The DamageSource.Type is hardcoded to EnumDamageType.Crushing.

------------------------------
## 2. The Armor Bypass Gate (ModSystemWearableStats.handleDamaged)
The damage payload travels through the entity behavior system and hits the armor calculation loop inside ModSystemWearableStats. This is the exact function where the armor reduction is skipped:

```cs
private float handleDamaged(IPlayer player, float damage, DamageSource dmgSource)
{
    EnumDamageType type = dmgSource.Type;
    damage = this.applyShieldProtection(player, damage, dmgSource);
    if (damage <= 0f)
    {
        return 0f;
    }
    if (this.api.Side == EnumAppSide.Client)
    {
        return damage;
    }
    // --- THE GATEKEEPER LINE ---
    if (type != EnumDamageType.BluntAttack && type != EnumDamageType.PiercingAttack && type != EnumDamageType.SlashingAttack)
    {
        return damage; // Crushing exits here! No armor math is ever run.
    }
    // ... (Vanilla Armor Calculations happen down here)
```

Because EnumDamageType.Crushing does not match the three whitelisted physical attack types, the method immediately returns the raw, unmodified damage.
------------------------------
## 3. Summary Call Flow

   1. EntityBlockFalling.OnGameTick calculates velocity-based damage and triggers entity.ReceiveDamage with EnumDamageType.Crushing.
   2. Entity.ReceiveDamage passes the damage to all attached entity behaviors.
   3. EntityBehaviorHealth.OnEntityReceiveDamage receives it and fires the ApplyOnDamageDelegates loop.
   4. ModSystemWearableStats.handleDamaged runs inside that loop, sees that Crushing is not an attack type, and exits early, applying 0% armor protection and 0 durability damage.
   5. EntityBehaviorHealth subtracts the original, unmitigated damage value directly from the player's health pool (this.Health -= damage;).

If you were to change the DamageSource.Type to an attack type like EnumDamageType.BluntAttack right before Step 4, the execution would slip right past that if gate and natively process the vanilla armor tier, flat reduction, and relative protection math.
