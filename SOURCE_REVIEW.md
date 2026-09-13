# Source Review

VRates is intentionally small and managed-only so its runtime behavior is easy to inspect.

## Supported settings

VRates v1.0.0 modifies exactly five V Rising settings:

```text
MaterialYieldModifier_Global
DropTableModifier_General
DropTableModifier_Missions
DropTableModifier_StygianShards
BloodEssenceYieldModifier
```

It does not modify inventory stack size, crafting rates, refinement rates, or unrelated server settings.

## Standalone runtime modification

When VStacks is not installed:

1. VRates pattern-scans the executable sections of the loaded `GameAssembly.dll` for the current `SettingsClamp::Half` signature.
2. The signature must be found exactly once; otherwise the hook is not installed.
3. BepInEx `INativeDetour.CreateAndApply` installs the hook from managed C#.
4. The detour compares the IL2CPP field name against the five exact supported names.
5. Only a matching supported setting receives its configured VRates multiplier.
6. Every unrelated setting passes through unchanged.

## VStacks compatibility

VRates declares `com.originera.vstack` as a BepInEx soft dependency.

When VStacks 1.0.1+ is present, VRates registers all five exact setting names through VStacks' public cooperative settings-hook API and does not install its own native detour.

This prevents two plugins from rewriting the same `SettingsClamp::Half` function entry.

If an older VStacks build is detected without the compatibility API, VRates fails safely instead of installing a competing hook.

## No bundled native payload

The package does not contain:

```text
VRates.Native.dll
MinHook
version.dll
```

## Configuration

```text
BepInEx/config/VRates.cfg
```

Default values:

```ini
[Rates]
HarvestMultiplier = 10
LootMultiplier = 10
MissionLootMultiplier = 10
StygianShardMultiplier = 10
BloodEssenceMultiplier = 10
```

## Build provenance

The Thunderstore package includes:

```text
Source/Plugin.cs
Source/VRates.csproj
Source/BUILD_COMMIT.txt
```

`BUILD_COMMIT.txt` contains the Git commit hash used by GitHub Actions to build the packaged DLL.
