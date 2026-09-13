# Source Review

VRates is intentionally small and managed-only so its runtime behavior is easy to inspect.

## Runtime modification

VRates performs one production runtime modification:

1. It pattern-scans the executable sections of the already-loaded `GameAssembly.dll` for the known current `SettingsClamp::Half` signature.
2. The signature must be found exactly once; otherwise the hook is not installed.
3. BepInEx `INativeDetour.CreateAndApply` is used to detour that function from managed C#.
4. Inside the detour, the IL2CPP field name is compared against exactly two strings:
   - `MaterialYieldModifier_Global`
   - `DropTableModifier_General`
5. Only those two settings receive the configured VRates values.
6. Every unrelated server setting is passed to the original function unchanged.

## No broad settings patch

VRates v1.0.0 intentionally does not modify mission loot, Stygian Shards, Blood Essence, stack size, crafting rates, refinement rates, or unrelated server settings.

## No bundled native payload

The package does not contain:

```text
VRates.Native.dll
MinHook
version.dll
```

The native detour is provided by BepInEx's own IL2CPP hook API.

## Configuration

The only user-facing configuration is:

```text
BepInEx/config/VRates.cfg
```

with:

```ini
[Rates]
HarvestMultiplier = 10
LootMultiplier = 10
```

## Build provenance

The Thunderstore package includes:

```text
Source/Plugin.cs
Source/VRates.csproj
Source/BUILD_COMMIT.txt
```

`BUILD_COMMIT.txt` contains the Git commit hash used by GitHub Actions to build the packaged DLL.

## VStacks compatibility

VRates declares `com.originera.vstack` as a BepInEx soft dependency. When VStacks 1.0.1+ is present, VRates
registers its two exact setting names through VStacks' public cooperative settings-hook API and does not install
its own native detour. When VStacks is absent, VRates uses its standalone `INativeDetour` path.

This prevents two plugins from rewriting the same `SettingsClamp::Half` function entry.
