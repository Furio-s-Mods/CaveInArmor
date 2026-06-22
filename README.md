# CaveInArmor

A lightweight, purely server-side C# code mod for **Vintage Story** that allows vanilla armor to dynamically mitigate, absorb, and process crushing damage from cave-ins, landslides and falling blocks.

---

## Features

* **Purely Server-Side:** Clients don't need to download anything. Just drop it into the server's mod folder.
* **No New Assets:** Reads protection tiers, flat damage reductions, and relative percentages directly from equipped vanilla armor.
* **Dynamic Modifiers:** Armor degrades on impact, playing tool-breaking sound cues if a catastrophic cave-in shatters a piece of gear.
* **Works Universally:** Applies to soil, gravel, sand, and all other falling blocks.

---

## Configuration (`caveinarmorConfig.json`)

Upon the first server boot, a configuration file will be generated inside your `ModConfig` directory.
Automatic file upgrade for new versions, obsolete files are kept with .bad extension.

### Protection Modes

1. **Lottery Mode (`"UseLayered": false`)**
   Mirrors native vanilla combat mechanics. Falling blocks roll a random lottery to hit one specific body slot based on standard game weights (20% Head, 50% Torso, 30% Legs). Only the targeted piece mitigates damage and loses durability.

2. **Layered Mode (`"UseLayered": true`)**
   A specialized "mining safety" experience. Boulders strike your entire body sequentially, filtering damage from top to bottom.
   3 possible situations:
    - VERTICAL fall: `Vertical`: {
      * `LayeredHeadMultiplier`: Helmet effectiveness (Default: `1.0` - 100%)
      * `LayeredTorsoMultiplier`: Chestplate effectiveness (Default: `0.5` - 50%)
      * `LayeredLegsMultiplier`: Greaves effectiveness (Default: `0.1` - 10%)
    }
    - VERTICAL bounce: `Horizontal`: {
      * `LayeredHeadMultiplier`: Helmet effectiveness (Default: `0.0` - 0%)
      * `LayeredTorsoMultiplier`: Chestplate effectiveness (Default: `0.5` - 50%)
      * `LayeredLegsMultiplier`: Greaves effectiveness (Default: `1.0` - 100%)
    }
    - UNDER_FEET slide: uses `Vertical`.`LayeredLegsMultiplier`.

### Global Parameters

* `ConfigVersion`: File verion number for integrity check.
* `Enabled`: Instantly toggle the mod on or off.
* `EnableDebugLogging`: Outputs real-time mitigation data directly to the server console.
* `DurabilityDamageMultiplier`: Scales durability loss (Set to `0.0` for unbreakable protection).
* `MinimumDamageThreshold`: The minimum damage a player will always take, preventing complete immunity.


### How To Use
- [see mod page](https://mods.vintagestory.at/show/mod/52223)
---

## Contribution & Development

Want to contribute code or compile this mod locally? Please review the central [Contributing Guidelines](https://github.com/Furio-s-Mods/.github/blob/main/CONTRIBUTING.md) for environment setup and path management instructions.

## Acknowledgements
* **[Anego Studios](https://anegostudios.com)** - Vintage Story Devs
* **AlteOgre** for the original idea for the [Ilu Ambar Server](https://www.vintagestory.at/forums/topic/15864-1222euen-ilu-ambar-quenta-y%C3%A1ra-whitelisted-coop-build-pve-focussed-modded-light-rp-new-player-friendly-difficulty-progression-unique/)


## License

[MIT License](LICENSE)

---
