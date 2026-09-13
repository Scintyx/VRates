# Source Review

VRates is intentionally managed-only and limits its runtime changes to explicitly named V Rising server settings.

## Supported settings

VRates v1.0.0 modifies exactly these 10 setting names:

```text
MaterialYieldModifier_Global
DropTableModifier_General
DropTableModifier_Missions
DropTableModifier_StygianShards
BloodEssenceYieldModifier
CraftRateModifier
RefinementRateModifier
RecipeCostModifier
RefinementCostModifier
BuildCostModifier
```

`InventoryStacksModifier` is not modified by VRates.

## Defaults

Rate/speed settings default to x10.

Cost settings default to x1:

```text
RecipeCostModifier
RefinementCostModifier
BuildCostModifier
```

## Standalone hook

When VStacks is absent:

1. VRates scans executable sections of the loaded `GameAssembly.dll`.
2. `SettingsClamp::Half` must match exactly once.
3. BepInEx `INativeDetour.CreateAndApply` installs one managed detour.
4. The incoming IL2CPP field name is compared against the 10 exact supported names.
5. Only an exact match is overridden.
6. All unrelated settings pass to the original function unchanged.

## VStacks compatibility

VRates declares `com.originera.vstack` as a BepInEx soft dependency.

With VStacks 1.0.1+, VRates registers the 10 exact setting names with VStacks' cooperative settings-hook API and does not install another native detour.

This avoids two mods rewriting the same native function entry.

## No native payload

The package contains no:

```text
VRates.Native.dll
MinHook
version.dll
```

## Build provenance

The Thunderstore package includes:

```text
Source/Plugin.cs
Source/VRates.csproj
Source/BUILD_COMMIT.txt
```
