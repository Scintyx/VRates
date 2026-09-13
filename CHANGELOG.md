# Changelog

## 1.0.0

- Initial public release.
- Added configurable x10-default:
  - Harvest
  - General Loot
  - Mission Loot
  - Stygian Shards
  - Blood Essence
  - Craft Speed
  - Refinement Speed
- Added configurable x1-default:
  - Recipe Cost
  - Refinement Cost
  - Build Cost
- Added cooperative compatibility with VStacks 1.0.1+ so both mods share one `SettingsClamp::Half` native hook.
- VRates remains standalone when VStacks is absent.
- Server-side only; connecting clients do not require VRates.
- No custom native DLL, MinHook, or proxy DLL.
