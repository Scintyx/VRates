# VRates

**VRates** is a managed-only BepInEx 6 IL2CPP mod for **V Rising** that extends common server rates and costs beyond the normal game-setting limits.

VRates v1.0.0 exposes **10 independently configurable settings**.

Rate/speed defaults are **x10**. Cost defaults remain **x1 (vanilla)**.

## Configuration

On first server/host launch, VRates creates:

```text
BepInEx/config/VRates.cfg
```

Default configuration:

```ini
[Rates]
HarvestMultiplier = 10
LootMultiplier = 10
MissionLootMultiplier = 10
StygianShardMultiplier = 10
BloodEssenceMultiplier = 10
CraftSpeedMultiplier = 10
RefinementSpeedMultiplier = 10

RecipeCostMultiplier = 1
RefinementCostMultiplier = 1
BuildCostMultiplier = 1
```

Restart the server/host after changing the file.

## Setting map

| Config option | V Rising setting | Default | Purpose |
|---|---|---:|---|
| `HarvestMultiplier` | `MaterialYieldModifier_Global` | x10 | Resource-node harvesting yield |
| `LootMultiplier` | `DropTableModifier_General` | x10 | General loot |
| `MissionLootMultiplier` | `DropTableModifier_Missions` | x10 | Servant mission rewards |
| `StygianShardMultiplier` | `DropTableModifier_StygianShards` | x10 | Stygian Shard drops |
| `BloodEssenceMultiplier` | `BloodEssenceYieldModifier` | x10 | Blood Essence yield |
| `CraftSpeedMultiplier` | `CraftRateModifier` | x10 | Crafting speed |
| `RefinementSpeedMultiplier` | `RefinementRateModifier` | x10 | Refinement/processing speed |
| `RecipeCostMultiplier` | `RecipeCostModifier` | x1 | Crafting recipe material cost |
| `RefinementCostMultiplier` | `RefinementCostModifier` | x1 | Refinement material cost |
| `BuildCostMultiplier` | `BuildCostModifier` | x1 | Building material cost |

### Cost multiplier examples

For the three cost settings:

```text
1.0 = normal vanilla cost
0.5 = approximately half material cost
2.0 = approximately double material cost
```

The mod accepts positive values from `0.01` up to `65504`.

## Intentionally not modified

VRates does not modify:

```text
InventoryStacksModifier
```

Inventory stack size remains the responsibility of VStacks.

## Compatibility with VStacks

VRates works by itself or together with **VStacks 1.0.1+**.

When VStacks is installed:

1. BepInEx loads VStacks first because VRates declares it as a soft dependency.
2. VStacks owns the single native `SettingsClamp::Half` detour.
3. VRates registers all 10 exact supported setting names with VStacks.
4. VRates does **not** install a second native detour.

When VStacks is absent, VRates installs its own standalone `SettingsClamp::Half` hook.

If VStacks 1.0.0 is detected, VRates refuses to install a competing native hook and asks for VStacks 1.0.1+.

## Installation

VRates is intended to be **server-side**.

### Dedicated server

Install:

```text
BepInEx/plugins/VRates/VRates.dll
```

Start the server once, then edit:

```text
BepInEx/config/VRates.cfg
```

Restart after changes.

Clients do not need VRates installed.

### Host & Play

Install VRates into the host's BepInEx environment.

## Implementation

When running standalone, VRates:

1. locates the current `SettingsClamp::Half` implementation in `GameAssembly.dll` using a fail-closed executable signature;
2. installs one managed BepInEx `INativeDetour`;
3. compares the IL2CPP field name against the 10 supported setting names;
4. substitutes the configured value only for an exact match;
5. passes every unrelated setting through unchanged.

When VStacks 1.0.1+ is present, VRates registers those exact names through VStacks' cooperative hook API instead.

VRates contains no custom native DLL, MinHook, or proxy DLL.

## Build

```bash
dotnet restore src/VRates/VRates.csproj --configfile nuget.config
dotnet build src/VRates/VRates.csproj -c Release --no-restore
```

Output:

```text
src/VRates/bin/Release/net6.0/VRates.dll
```
