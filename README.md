# VRates

**VRates** is a managed-only BepInEx 6 IL2CPP mod for **V Rising** that removes the normal x3 limit from five common server rate settings.

All five rates default to **x10** and are independently configurable.

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
```

Restart the server/host after changing the file.

Practical values such as x5, x10, x20, x50, or x100 are recommended.

## Rate mapping

| Config option | V Rising setting | Purpose |
|---|---|---|
| `HarvestMultiplier` | `MaterialYieldModifier_Global` | Resource-node harvesting yield |
| `LootMultiplier` | `DropTableModifier_General` | General loot from kills/chests/drop tables |
| `MissionLootMultiplier` | `DropTableModifier_Missions` | Servant mission rewards |
| `StygianShardMultiplier` | `DropTableModifier_StygianShards` | Stygian Shard drops |
| `BloodEssenceMultiplier` | `BloodEssenceYieldModifier` | Blood Essence yield |

VRates accepts positive values up to `65504`, the largest finite value representable by the half-precision setting type used by this V Rising settings path.

## Intentionally not modified

VRates v1.0.0 does **not** modify:

```text
InventoryStacksModifier
CraftRateModifier
RefinementRateModifier
```

VStacks continues to own inventory stack size separately.

## Compatibility with VStacks

VRates works by itself or together with **VStacks 1.0.1+**.

When VStacks is installed:

1. BepInEx loads VStacks first because VRates declares VStacks as a soft dependency.
2. VStacks owns the single native `SettingsClamp::Half` detour.
3. VRates registers its five exact setting names with VStacks.
4. VRates does **not** install a second native detour.

When VStacks is not installed, VRates runs in standalone mode and installs its own `SettingsClamp::Half` hook.

If VStacks 1.0.0 is detected, VRates intentionally refuses to install a competing hook and asks for VStacks 1.0.1+.

## Installation

VRates is intended to be **server-side**.

### Dedicated server

1. Install BepInExPack V Rising `1.733.2`.
2. Copy:

```text
VRates.dll
```

to:

```text
BepInEx/plugins/VRates/VRates.dll
```

3. Start the server once.
4. Edit:

```text
BepInEx/config/VRates.cfg
```

5. Restart the server.

Connecting players do not need VRates installed.

### Host & Play

Install VRates into the BepInEx environment used by the host game.

## How it works

V Rising validates these server multipliers through `SettingsClamp::Half`, where the normal server settings range is capped at x3.

When standalone, VRates:

1. locates the current `SettingsClamp::Half` implementation in `GameAssembly.dll` using a fail-closed executable signature;
2. installs a managed BepInEx `INativeDetour`;
3. checks the IL2CPP setting name;
4. substitutes the configured multiplier only for the five supported rate settings;
5. passes every unrelated setting to the original game function unchanged.

When VStacks 1.0.1+ is installed, VRates registers the same five exact names through VStacks' cooperative hook API instead.

VRates ships no custom native DLL, MinHook, or proxy DLL.

## Build

Requires the .NET 6 SDK.

```bash
dotnet restore src/VRates/VRates.csproj --configfile nuget.config
dotnet build src/VRates/VRates.csproj -c Release --no-restore
```

Output:

```text
src/VRates/bin/Release/net6.0/VRates.dll
```

## Source review

The Thunderstore package contains the exact `Plugin.cs` and `VRates.csproj` used by CI plus `Source/BUILD_COMMIT.txt`.

See `SOURCE_REVIEW.md` for the exact hook scope.
