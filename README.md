# VRates

**VRates** is a managed-only BepInEx 6 IL2CPP mod for **V Rising** that removes the normal x3 limit from the two most common resource-rate settings:

- `MaterialYieldModifier_Global` — harvesting/resource-node yield.
- `DropTableModifier_General` — general loot from kills, chests, and normal drop tables.

Both default to **x10** and are independently configurable.

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
```

Examples:

```ini
[Rates]
HarvestMultiplier = 20
LootMultiplier = 5
```

Restart the server/host after changing the file.

VRates accepts positive values up to `65504`, the largest finite value representable by the half-precision setting type used by this V Rising settings path. Extremely large multipliers may create excessive item drops or server load, so practical values such as x5, x10, x20, x50, or x100 are recommended.

## What each rate changes

### HarvestMultiplier

Targets only:

```text
MaterialYieldModifier_Global
```

This controls materials received from harvesting resource nodes.

### LootMultiplier

Targets only:

```text
DropTableModifier_General
```

This controls the game's general drop-table multiplier, including ordinary loot from kills/chests/drop tables.

## Intentionally not modified

VRates v1.0.0 does **not** change:

```text
DropTableModifier_Missions
DropTableModifier_StygianShards
BloodEssenceYieldModifier
InventoryStacksModifier
CraftRateModifier
RefinementRateModifier
```

Those settings are deliberately left untouched.

If additional rate categories are useful later, they can be added explicitly in a future release instead of globally uncapping unrelated server settings.

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

Players do not need VRates installed on their clients.

### Host & Play

Install VRates into the BepInEx environment used by the host game. Connecting players do not need the mod.

## How it works

V Rising normally validates these server multipliers through `SettingsClamp::Half`, where the normal settings range is capped at x3.

VRates:

1. locates the current `SettingsClamp::Half` implementation in `GameAssembly.dll` using a fail-closed executable signature;
2. installs a managed BepInEx `INativeDetour`;
3. checks the IL2CPP setting name;
4. substitutes the configured value only for:
   - `MaterialYieldModifier_Global`
   - `DropTableModifier_General`;
5. passes every unrelated setting through to the original game function unchanged.

VRates does not ship a custom native DLL, MinHook, or proxy DLL.

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

The Thunderstore package contains the exact `Plugin.cs` and `VRates.csproj` used by CI, plus `Source/BUILD_COMMIT.txt` containing the Git commit that produced the DLL.

See `SOURCE_REVIEW.md` for the runtime-hook scope.
