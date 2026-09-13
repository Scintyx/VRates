# Changelog

## 1.0.0

- Initial public release.
- Added configurable `HarvestMultiplier`.
- Added configurable `LootMultiplier`.
- Both multipliers default to x10.
- Overrides only `MaterialYieldModifier_Global` and `DropTableModifier_General`.
- Removes the normal x3 limit for those two settings through the managed BepInEx `SettingsClamp::Half` detour.
- Server-side only; connecting clients do not require VRates.
- No custom native DLL, MinHook, or proxy DLL.
