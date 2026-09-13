using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Hook;

namespace VRates;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.originera.vstack", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "com.originera.vrates";
    public const string PluginName = "VRates";
    public const string PluginVersion = "1.0.0";

    public const float DefaultHarvestMultiplier = 10.0f;
    public const float DefaultLootMultiplier = 10.0f;
    public const float DefaultMissionLootMultiplier = 10.0f;
    public const float DefaultStygianShardMultiplier = 10.0f;
    public const float DefaultBloodEssenceMultiplier = 10.0f;
    public const float DefaultCraftSpeedMultiplier = 10.0f;
    public const float DefaultRefinementSpeedMultiplier = 10.0f;

    // Cost multipliers intentionally default to vanilla x1.
    public const float DefaultRecipeCostMultiplier = 1.0f;
    public const float DefaultRefinementCostMultiplier = 1.0f;
    public const float DefaultBuildCostMultiplier = 1.0f;

    public const float MinimumMultiplier = 0.01f;

    // SettingsClamp::Half stores these values as IEEE 754 binary16/half.
    // 65504 is the largest finite positive value representable by binary16.
    public const float MaximumMultiplier = 65504.0f;

    private const string HarvestSettingName = "MaterialYieldModifier_Global";
    private const string LootSettingName = "DropTableModifier_General";
    private const string MissionLootSettingName = "DropTableModifier_Missions";
    private const string StygianShardSettingName = "DropTableModifier_StygianShards";
    private const string BloodEssenceSettingName = "BloodEssenceYieldModifier";
    private const string CraftRateSettingName = "CraftRateModifier";
    private const string RefinementRateSettingName = "RefinementRateModifier";
    private const string RecipeCostSettingName = "RecipeCostModifier";
    private const string RefinementCostSettingName = "RefinementCostModifier";
    private const string BuildCostSettingName = "BuildCostModifier";

    private const uint ImageScnMemExecute = 0x20000000;

    // Current V Rising SettingsClamp::Half signature.
    // Wildcards are represented by 0x00 in Pattern and false in PatternMask.
    private static readonly byte[] Pattern =
    {
        0x40, 0x57, 0x48, 0x83, 0xEC, 0x60, 0x80, 0x3D,
        0x00, 0x00, 0x00, 0x00, 0x00,
        0x49, 0x8B
    };

    private static readonly bool[] PatternMask =
    {
        true, true, true, true, true, true, true, true,
        false, false, false, false, false,
        true, true
    };

    private ConfigFile? _vRatesConfig;

    private ConfigEntry<float>? _harvestMultiplier;
    private ConfigEntry<float>? _lootMultiplier;
    private ConfigEntry<float>? _missionLootMultiplier;
    private ConfigEntry<float>? _stygianShardMultiplier;
    private ConfigEntry<float>? _bloodEssenceMultiplier;
    private ConfigEntry<float>? _craftSpeedMultiplier;
    private ConfigEntry<float>? _refinementSpeedMultiplier;
    private ConfigEntry<float>? _recipeCostMultiplier;
    private ConfigEntry<float>? _refinementCostMultiplier;
    private ConfigEntry<float>? _buildCostMultiplier;

    private INativeDetour? _detour;
    private SettingsClampHalfDelegate? _original;
    private SettingsClampHalfDelegate? _detourDelegate;

    private bool _loggedHarvestIntercept;
    private bool _loggedLootIntercept;
    private bool _loggedMissionLootIntercept;
    private bool _loggedStygianShardIntercept;
    private bool _loggedBloodEssenceIntercept;
    private bool _loggedCraftSpeedIntercept;
    private bool _loggedRefinementSpeedIntercept;
    private bool _loggedRecipeCostIntercept;
    private bool _loggedRefinementCostIntercept;
    private bool _loggedBuildCostIntercept;

    private bool _loggedInvalidHarvest;
    private bool _loggedInvalidLoot;
    private bool _loggedInvalidMissionLoot;
    private bool _loggedInvalidStygianShard;
    private bool _loggedInvalidBloodEssence;
    private bool _loggedInvalidCraftSpeed;
    private bool _loggedInvalidRefinementSpeed;
    private bool _loggedInvalidRecipeCost;
    private bool _loggedInvalidRefinementCost;
    private bool _loggedInvalidBuildCost;

    private bool _usingVStackBroker;
    private MethodInfo? _vStackUnregisterMethod;

    private Func<float>? _harvestProviderDelegate;
    private Func<float>? _lootProviderDelegate;
    private Func<float>? _missionLootProviderDelegate;
    private Func<float>? _stygianShardProviderDelegate;
    private Func<float>? _bloodEssenceProviderDelegate;
    private Func<float>? _craftSpeedProviderDelegate;
    private Func<float>? _refinementSpeedProviderDelegate;
    private Func<float>? _recipeCostProviderDelegate;
    private Func<float>? _refinementCostProviderDelegate;
    private Func<float>? _buildCostProviderDelegate;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ushort SettingsClampHalfDelegate(float value, float min, float max, IntPtr fieldName);

    public override void Load()
    {
        _vRatesConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "VRates.cfg"), true);

        _harvestMultiplier = BindRate(
            "HarvestMultiplier",
            DefaultHarvestMultiplier,
            "Multiplier for MaterialYieldModifier_Global (resource-node harvesting).");

        _lootMultiplier = BindRate(
            "LootMultiplier",
            DefaultLootMultiplier,
            "Multiplier for DropTableModifier_General (general loot from kills/chests/drop tables).");

        _missionLootMultiplier = BindRate(
            "MissionLootMultiplier",
            DefaultMissionLootMultiplier,
            "Multiplier for DropTableModifier_Missions (servant mission rewards).");

        _stygianShardMultiplier = BindRate(
            "StygianShardMultiplier",
            DefaultStygianShardMultiplier,
            "Multiplier for DropTableModifier_StygianShards (Stygian Shard drops).");

        _bloodEssenceMultiplier = BindRate(
            "BloodEssenceMultiplier",
            DefaultBloodEssenceMultiplier,
            "Multiplier for BloodEssenceYieldModifier (Blood Essence yield).");

        _craftSpeedMultiplier = BindRate(
            "CraftSpeedMultiplier",
            DefaultCraftSpeedMultiplier,
            "Multiplier for CraftRateModifier (crafting speed).");

        _refinementSpeedMultiplier = BindRate(
            "RefinementSpeedMultiplier",
            DefaultRefinementSpeedMultiplier,
            "Multiplier for RefinementRateModifier (refinement/processing speed).");

        _recipeCostMultiplier = BindRate(
            "RecipeCostMultiplier",
            DefaultRecipeCostMultiplier,
            "Multiplier for RecipeCostModifier (crafting recipe material cost). x1 is vanilla.");

        _refinementCostMultiplier = BindRate(
            "RefinementCostMultiplier",
            DefaultRefinementCostMultiplier,
            "Multiplier for RefinementCostModifier (refinement material cost). x1 is vanilla.");

        _buildCostMultiplier = BindRate(
            "BuildCostMultiplier",
            DefaultBuildCostMultiplier,
            "Multiplier for BuildCostModifier (building material cost). x1 is vanilla.");

        bool vStackDetected;
        if (TryRegisterWithVStack(out vStackDetected))
        {
            Log.LogInfo($"{PluginName} {PluginVersion} loaded using the VStacks shared SettingsClamp hook.");
            LogConfiguredRates();
            Log.LogInfo($"Config file: {_vRatesConfig.ConfigFilePath}");
            return;
        }

        if (vStackDetected)
        {
            Log.LogError(
                "VStacks was detected, but it does not expose the shared SettingsClamp compatibility API. " +
                "Update VStacks to version 1.0.1 or newer. VRates will not install a second competing native hook.");
            return;
        }

        // Standalone mode: when VStacks is not installed, VRates owns the native
        // SettingsClamp::Half detour.
        try
        {
            IntPtr target = FindSettingsClampHalf();
            if (target == IntPtr.Zero)
            {
                Log.LogError(
                    "SettingsClamp::Half signature was not found. " +
                    "The game may have updated; VRates was not applied.");
                return;
            }

            SettingsClampHalfDelegate detourDelegate = SettingsClampHalfDetour;
            _detourDelegate = detourDelegate;

            _detour = INativeDetour.CreateAndApply<SettingsClampHalfDelegate>(
                target,
                detourDelegate,
                out var original);

            _original = original;

            Log.LogInfo($"{PluginName} {PluginVersion} loaded (managed-only BepInEx hook).");
            Log.LogInfo($"Hooked SettingsClamp::Half at 0x{target.ToInt64():X}.");
            LogConfiguredRates();
            Log.LogInfo($"Config file: {_vRatesConfig.ConfigFilePath}");
            Log.LogInfo(
                "VRates modifies only its ten explicitly supported rate/cost settings. " +
                "All other V Rising settings pass through unchanged.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to install SettingsClamp::Half detour: {ex}");
        }
    }

    public override bool Unload()
    {
        try
        {
            if (_usingVStackBroker && _vStackUnregisterMethod is not null)
                _vStackUnregisterMethod.Invoke(null, new object[] { PluginGuid });
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Error while unregistering from the VStacks settings broker: {ex}");
        }

        try
        {
            _usingVStackBroker = false;
            _vStackUnregisterMethod = null;

            _harvestProviderDelegate = null;
            _lootProviderDelegate = null;
            _missionLootProviderDelegate = null;
            _stygianShardProviderDelegate = null;
            _bloodEssenceProviderDelegate = null;
            _craftSpeedProviderDelegate = null;
            _refinementSpeedProviderDelegate = null;
            _recipeCostProviderDelegate = null;
            _refinementCostProviderDelegate = null;
            _buildCostProviderDelegate = null;

            _detour?.Dispose();
            _detour = null;
            _original = null;
            _detourDelegate = null;

            _harvestMultiplier = null;
            _lootMultiplier = null;
            _missionLootMultiplier = null;
            _stygianShardMultiplier = null;
            _bloodEssenceMultiplier = null;
            _craftSpeedMultiplier = null;
            _refinementSpeedMultiplier = null;
            _recipeCostMultiplier = null;
            _refinementCostMultiplier = null;
            _buildCostMultiplier = null;
            _vRatesConfig = null;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Error while removing native detour: {ex}");
        }

        return true;
    }

    private ConfigEntry<float> BindRate(string key, float defaultValue, string description)
    {
        if (_vRatesConfig is null)
            throw new InvalidOperationException("VRates config is not initialized.");

        return _vRatesConfig.Bind(
            "Rates",
            key,
            defaultValue,
            description + " " +
            $"Default: {defaultValue:0.###}. " +
            "Valid positive range: 0.01 to 65504. Restart the server/host after editing.");
    }

    private void LogConfiguredRates()
    {
        Log.LogInfo(
            $"Harvest x{GetHarvestMultiplier():0.###}; " +
            $"General Loot x{GetLootMultiplier():0.###}; " +
            $"Mission Loot x{GetMissionLootMultiplier():0.###}; " +
            $"Stygian Shards x{GetStygianShardMultiplier():0.###}; " +
            $"Blood Essence x{GetBloodEssenceMultiplier():0.###}; " +
            $"Craft Speed x{GetCraftSpeedMultiplier():0.###}; " +
            $"Refinement Speed x{GetRefinementSpeedMultiplier():0.###}; " +
            $"Recipe Cost x{GetRecipeCostMultiplier():0.###}; " +
            $"Refinement Cost x{GetRefinementCostMultiplier():0.###}; " +
            $"Build Cost x{GetBuildCostMultiplier():0.###}.");
    }

    private bool TryRegisterWithVStack(out bool vStackDetected)
    {
        vStackDetected = false;
        Type? vStackPluginType = null;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? candidate = assembly.GetType("VStack.Plugin", throwOnError: false, ignoreCase: false);
            if (candidate is null)
                continue;

            vStackPluginType = candidate;
            vStackDetected = true;
            break;
        }

        if (vStackPluginType is null)
            return false;

        MethodInfo? registerMethod = vStackPluginType.GetMethod(
            "RegisterSettingOverride",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(Func<float>) },
            modifiers: null);

        MethodInfo? unregisterMethod = vStackPluginType.GetMethod(
            "UnregisterSettingOverrides",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);

        if (registerMethod is null || unregisterMethod is null)
            return false;

        _harvestProviderDelegate = GetHarvestMultiplier;
        _lootProviderDelegate = GetLootMultiplier;
        _missionLootProviderDelegate = GetMissionLootMultiplier;
        _stygianShardProviderDelegate = GetStygianShardMultiplier;
        _bloodEssenceProviderDelegate = GetBloodEssenceMultiplier;
        _craftSpeedProviderDelegate = GetCraftSpeedMultiplier;
        _refinementSpeedProviderDelegate = GetRefinementSpeedMultiplier;
        _recipeCostProviderDelegate = GetRecipeCostMultiplier;
        _refinementCostProviderDelegate = GetRefinementCostMultiplier;
        _buildCostProviderDelegate = GetBuildCostMultiplier;

        try
        {
            bool allRegistered =
                RegisterWithBroker(registerMethod, HarvestSettingName, _harvestProviderDelegate) &&
                RegisterWithBroker(registerMethod, LootSettingName, _lootProviderDelegate) &&
                RegisterWithBroker(registerMethod, MissionLootSettingName, _missionLootProviderDelegate) &&
                RegisterWithBroker(registerMethod, StygianShardSettingName, _stygianShardProviderDelegate) &&
                RegisterWithBroker(registerMethod, BloodEssenceSettingName, _bloodEssenceProviderDelegate) &&
                RegisterWithBroker(registerMethod, CraftRateSettingName, _craftSpeedProviderDelegate) &&
                RegisterWithBroker(registerMethod, RefinementRateSettingName, _refinementSpeedProviderDelegate) &&
                RegisterWithBroker(registerMethod, RecipeCostSettingName, _recipeCostProviderDelegate) &&
                RegisterWithBroker(registerMethod, RefinementCostSettingName, _refinementCostProviderDelegate) &&
                RegisterWithBroker(registerMethod, BuildCostSettingName, _buildCostProviderDelegate);

            if (!allRegistered)
            {
                unregisterMethod.Invoke(null, new object[] { PluginGuid });
                return false;
            }

            _vStackUnregisterMethod = unregisterMethod;
            _usingVStackBroker = true;

            Log.LogInfo(
                "VStacks compatibility detected. Registered all ten VRates settings with the shared " +
                "SettingsClamp::Half hook; VRates will not install a second native detour.");

            return true;
        }
        catch
        {
            try
            {
                unregisterMethod.Invoke(null, new object[] { PluginGuid });
            }
            catch
            {
                // Best-effort rollback only.
            }

            _usingVStackBroker = false;
            _vStackUnregisterMethod = null;

            _harvestProviderDelegate = null;
            _lootProviderDelegate = null;
            _missionLootProviderDelegate = null;
            _stygianShardProviderDelegate = null;
            _bloodEssenceProviderDelegate = null;
            _craftSpeedProviderDelegate = null;
            _refinementSpeedProviderDelegate = null;
            _recipeCostProviderDelegate = null;
            _refinementCostProviderDelegate = null;
            _buildCostProviderDelegate = null;

            throw;
        }
    }

    private static bool RegisterWithBroker(
        MethodInfo registerMethod,
        string settingName,
        Func<float> provider)
    {
        return Convert.ToBoolean(
            registerMethod.Invoke(
                null,
                new object[]
                {
                    PluginGuid,
                    settingName,
                    provider
                }));
    }

    private ushort SettingsClampHalfDetour(float value, float min, float max, IntPtr fieldName)
    {
        SettingsClampHalfDelegate? original = _original;
        if (original is null)
            return 0;

        if (IsIl2CppStringEqual(fieldName, HarvestSettingName))
            ApplySetting(ref value, ref min, ref max, GetHarvestMultiplier(), HarvestSettingName, ref _loggedHarvestIntercept);
        else if (IsIl2CppStringEqual(fieldName, LootSettingName))
            ApplySetting(ref value, ref min, ref max, GetLootMultiplier(), LootSettingName, ref _loggedLootIntercept);
        else if (IsIl2CppStringEqual(fieldName, MissionLootSettingName))
            ApplySetting(ref value, ref min, ref max, GetMissionLootMultiplier(), MissionLootSettingName, ref _loggedMissionLootIntercept);
        else if (IsIl2CppStringEqual(fieldName, StygianShardSettingName))
            ApplySetting(ref value, ref min, ref max, GetStygianShardMultiplier(), StygianShardSettingName, ref _loggedStygianShardIntercept);
        else if (IsIl2CppStringEqual(fieldName, BloodEssenceSettingName))
            ApplySetting(ref value, ref min, ref max, GetBloodEssenceMultiplier(), BloodEssenceSettingName, ref _loggedBloodEssenceIntercept);
        else if (IsIl2CppStringEqual(fieldName, CraftRateSettingName))
            ApplySetting(ref value, ref min, ref max, GetCraftSpeedMultiplier(), CraftRateSettingName, ref _loggedCraftSpeedIntercept);
        else if (IsIl2CppStringEqual(fieldName, RefinementRateSettingName))
            ApplySetting(ref value, ref min, ref max, GetRefinementSpeedMultiplier(), RefinementRateSettingName, ref _loggedRefinementSpeedIntercept);
        else if (IsIl2CppStringEqual(fieldName, RecipeCostSettingName))
            ApplySetting(ref value, ref min, ref max, GetRecipeCostMultiplier(), RecipeCostSettingName, ref _loggedRecipeCostIntercept);
        else if (IsIl2CppStringEqual(fieldName, RefinementCostSettingName))
            ApplySetting(ref value, ref min, ref max, GetRefinementCostMultiplier(), RefinementCostSettingName, ref _loggedRefinementCostIntercept);
        else if (IsIl2CppStringEqual(fieldName, BuildCostSettingName))
            ApplySetting(ref value, ref min, ref max, GetBuildCostMultiplier(), BuildCostSettingName, ref _loggedBuildCostIntercept);

        // Every unrelated setting is passed to the original function unchanged.
        return original(value, min, max, fieldName);
    }

    private void ApplySetting(
        ref float value,
        ref float min,
        ref float max,
        float multiplier,
        string settingName,
        ref bool logged)
    {
        value = multiplier;
        min = 0.0f;
        max = multiplier;

        if (!logged)
        {
            logged = true;
            Log.LogInfo($"{settingName} intercepted and forced to x{multiplier:0.###}.");
        }
    }

    private float GetHarvestMultiplier() =>
        GetValidatedMultiplier(_harvestMultiplier?.Value ?? DefaultHarvestMultiplier, DefaultHarvestMultiplier, "HarvestMultiplier", ref _loggedInvalidHarvest);

    private float GetLootMultiplier() =>
        GetValidatedMultiplier(_lootMultiplier?.Value ?? DefaultLootMultiplier, DefaultLootMultiplier, "LootMultiplier", ref _loggedInvalidLoot);

    private float GetMissionLootMultiplier() =>
        GetValidatedMultiplier(_missionLootMultiplier?.Value ?? DefaultMissionLootMultiplier, DefaultMissionLootMultiplier, "MissionLootMultiplier", ref _loggedInvalidMissionLoot);

    private float GetStygianShardMultiplier() =>
        GetValidatedMultiplier(_stygianShardMultiplier?.Value ?? DefaultStygianShardMultiplier, DefaultStygianShardMultiplier, "StygianShardMultiplier", ref _loggedInvalidStygianShard);

    private float GetBloodEssenceMultiplier() =>
        GetValidatedMultiplier(_bloodEssenceMultiplier?.Value ?? DefaultBloodEssenceMultiplier, DefaultBloodEssenceMultiplier, "BloodEssenceMultiplier", ref _loggedInvalidBloodEssence);

    private float GetCraftSpeedMultiplier() =>
        GetValidatedMultiplier(_craftSpeedMultiplier?.Value ?? DefaultCraftSpeedMultiplier, DefaultCraftSpeedMultiplier, "CraftSpeedMultiplier", ref _loggedInvalidCraftSpeed);

    private float GetRefinementSpeedMultiplier() =>
        GetValidatedMultiplier(_refinementSpeedMultiplier?.Value ?? DefaultRefinementSpeedMultiplier, DefaultRefinementSpeedMultiplier, "RefinementSpeedMultiplier", ref _loggedInvalidRefinementSpeed);

    private float GetRecipeCostMultiplier() =>
        GetValidatedMultiplier(_recipeCostMultiplier?.Value ?? DefaultRecipeCostMultiplier, DefaultRecipeCostMultiplier, "RecipeCostMultiplier", ref _loggedInvalidRecipeCost);

    private float GetRefinementCostMultiplier() =>
        GetValidatedMultiplier(_refinementCostMultiplier?.Value ?? DefaultRefinementCostMultiplier, DefaultRefinementCostMultiplier, "RefinementCostMultiplier", ref _loggedInvalidRefinementCost);

    private float GetBuildCostMultiplier() =>
        GetValidatedMultiplier(_buildCostMultiplier?.Value ?? DefaultBuildCostMultiplier, DefaultBuildCostMultiplier, "BuildCostMultiplier", ref _loggedInvalidBuildCost);

    private float GetValidatedMultiplier(
        float configured,
        float defaultValue,
        string settingName,
        ref bool loggedInvalid)
    {
        if (float.IsNaN(configured) || float.IsInfinity(configured) || configured <= 0.0f)
        {
            if (!loggedInvalid)
            {
                loggedInvalid = true;
                Log.LogWarning(
                    $"Invalid {settingName} '{configured}'. Using default x{defaultValue:0.###}.");
            }

            return defaultValue;
        }

        if (configured > MaximumMultiplier)
        {
            if (!loggedInvalid)
            {
                loggedInvalid = true;
                Log.LogWarning(
                    $"{settingName} x{configured:0.###} exceeds the half-precision limit. " +
                    $"Using x{MaximumMultiplier:0.###}.");
            }

            return MaximumMultiplier;
        }

        return Math.Max(configured, MinimumMultiplier);
    }

    private static unsafe bool IsIl2CppStringEqual(IntPtr stringObject, string expected)
    {
        if (stringObject == IntPtr.Zero)
            return false;

        byte* raw = (byte*)stringObject;
        int length = *(int*)(raw + 0x10);

        if (length != expected.Length || length < 0 || length > 128)
            return false;

        char* chars = (char*)(raw + 0x14);

        for (int i = 0; i < length; i++)
        {
            if (chars[i] != expected[i])
                return false;
        }

        return true;
    }

    private unsafe IntPtr FindSettingsClampHalf()
    {
        ProcessModule? gameAssembly = null;

        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (string.Equals(module.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
            {
                gameAssembly = module;
                break;
            }
        }

        if (gameAssembly is null)
            throw new InvalidOperationException("GameAssembly.dll is not loaded.");

        byte* imageBase = (byte*)gameAssembly.BaseAddress;

        if (*(ushort*)imageBase != 0x5A4D)
            throw new InvalidOperationException("Loaded GameAssembly.dll has an invalid DOS header.");

        int peOffset = *(int*)(imageBase + 0x3C);
        byte* ntHeaders = imageBase + peOffset;

        if (*(uint*)ntHeaders != 0x00004550)
            throw new InvalidOperationException("Loaded GameAssembly.dll has an invalid PE header.");

        ushort numberOfSections = *(ushort*)(ntHeaders + 0x06);
        ushort optionalHeaderSize = *(ushort*)(ntHeaders + 0x14);
        byte* section = ntHeaders + 0x18 + optionalHeaderSize;

        IntPtr found = IntPtr.Zero;
        int matches = 0;

        for (int i = 0; i < numberOfSections; i++, section += 40)
        {
            uint virtualSize = *(uint*)(section + 0x08);
            uint virtualAddress = *(uint*)(section + 0x0C);
            uint characteristics = *(uint*)(section + 0x24);

            if ((characteristics & ImageScnMemExecute) == 0 || virtualSize < Pattern.Length)
                continue;

            byte* start = imageBase + virtualAddress;
            int length = checked((int)virtualSize);

            for (int offset = 0; offset <= length - Pattern.Length; offset++)
            {
                if (!Matches(start + offset))
                    continue;

                matches++;
                found = (IntPtr)(start + offset);
            }
        }

        if (matches == 0)
            return IntPtr.Zero;

        if (matches != 1)
        {
            throw new InvalidOperationException(
                $"SettingsClamp::Half signature was not unique ({matches} matches). Refusing to hook.");
        }

        return found;
    }

    private static unsafe bool Matches(byte* address)
    {
        for (int i = 0; i < Pattern.Length; i++)
        {
            if (PatternMask[i] && address[i] != Pattern[i])
                return false;
        }

        return true;
    }
}
