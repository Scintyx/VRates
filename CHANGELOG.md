# Changelog

## 1.0.0

- Initial public release.
- Added configurable `HarvestMultiplier`, default x10.
- Added configurable `LootMultiplier`, default x10.
- Added configurable `MissionLootMultiplier`, default x10.
- Added configurable `StygianShardMultiplier`, default x10.
- Added configurable `BloodEssenceMultiplier`, default x10.
- Overrides only:
  - `MaterialYieldModifier_Global`
  - `DropTableModifier_General`
  - `DropTableModifier_Missions`
  - `DropTableModifier_StygianShards`
  - `BloodEssenceYieldModifier`
- Added cooperative compatibility with VStacks 1.0.1+ so both mods share one `SettingsClamp::Half` native hook.
- VRates remains standalone when VStacks is absent.
- Server-side only; connecting clients do not require VRates.
- No custom native DLL, MinHook, or proxy DLL.
