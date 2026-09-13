using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Hook;

namespace VRates;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "com.originera.vrates";
    public const string PluginName = "VRates";
    public const string PluginVersion = "1.0.0";

    public const float DefaultHarvestMultiplier = 10.0f;
    public const float DefaultLootMultiplier = 10.0f;
    public const float MinimumMultiplier = 0.01f;

    // SettingsClamp::Half stores these values as IEEE 754 binary16/half.
    // 65504 is the largest finite positive value representable by binary16.
    public const float MaximumMultiplier = 65504.0f;

    private const string HarvestSettingName = "MaterialYieldModifier_Global";
    private const string LootSettingName = "DropTableModifier_General";

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

    private INativeDetour? _detour;
    private SettingsClampHalfDelegate? _original;
    private SettingsClampHalfDelegate? _detourDelegate;

    private bool _loggedHarvestIntercept;
    private bool _loggedLootIntercept;
    private bool _loggedInvalidHarvest;
    private bool _loggedInvalidLoot;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ushort SettingsClampHalfDelegate(float value, float min, float max, IntPtr fieldName);

    public override void Load()
    {
        // Keep configuration in one predictable server-side file:
        // BepInEx/config/VRates.cfg
        _vRatesConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "VRates.cfg"), true);

        _harvestMultiplier = _vRatesConfig.Bind(
            "Rates",
            "HarvestMultiplier",
            DefaultHarvestMultiplier,
            "Multiplier for MaterialYieldModifier_Global (resource-node harvesting). " +
            "Default: 10. Examples: 5, 10, 20, 50, 100. " +
            "Valid positive range: 0.01 to 65504. Restart the server/host after editing.");

        _lootMultiplier = _vRatesConfig.Bind(
            "Rates",
            "LootMultiplier",
            DefaultLootMultiplier,
            "Multiplier for DropTableModifier_General (general loot from kills/chests/drop tables). " +
            "Default: 10. Examples: 5, 10, 20, 50, 100. " +
            "Valid positive range: 0.01 to 65504. Restart the server/host after editing.");

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
            Log.LogInfo(
                $"Harvest: {HarvestSettingName} -> x{GetHarvestMultiplier():0.###}; " +
                $"Loot: {LootSettingName} -> x{GetLootMultiplier():0.###}.");
            Log.LogInfo($"Config file: {_vRatesConfig.ConfigFilePath}");
            Log.LogInfo(
                "Only MaterialYieldModifier_Global and DropTableModifier_General are modified. " +
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
            _detour?.Dispose();
            _detour = null;
            _original = null;
            _detourDelegate = null;

            _harvestMultiplier = null;
            _lootMultiplier = null;
            _vRatesConfig = null;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Error while removing native detour: {ex}");
        }

        return true;
    }

    private ushort SettingsClampHalfDetour(float value, float min, float max, IntPtr fieldName)
    {
        SettingsClampHalfDelegate? original = _original;
        if (original is null)
            return 0;

        if (IsIl2CppStringEqual(fieldName, HarvestSettingName))
        {
            float multiplier = GetHarvestMultiplier();

            // Replace only MaterialYieldModifier_Global.
            value = multiplier;
            min = 0.0f;
            max = multiplier;

            if (!_loggedHarvestIntercept)
            {
                _loggedHarvestIntercept = true;
                Log.LogInfo($"{HarvestSettingName} intercepted and forced to x{multiplier:0.###}.");
            }
        }
        else if (IsIl2CppStringEqual(fieldName, LootSettingName))
        {
            float multiplier = GetLootMultiplier();

            // Replace only DropTableModifier_General.
            value = multiplier;
            min = 0.0f;
            max = multiplier;

            if (!_loggedLootIntercept)
            {
                _loggedLootIntercept = true;
                Log.LogInfo($"{LootSettingName} intercepted and forced to x{multiplier:0.###}.");
            }
        }

        // Every unrelated setting is passed to the original function unchanged.
        return original(value, min, max, fieldName);
    }

    private float GetHarvestMultiplier()
    {
        return GetValidatedMultiplier(
            _harvestMultiplier?.Value ?? DefaultHarvestMultiplier,
            DefaultHarvestMultiplier,
            "HarvestMultiplier",
            ref _loggedInvalidHarvest);
    }

    private float GetLootMultiplier()
    {
        return GetValidatedMultiplier(
            _lootMultiplier?.Value ?? DefaultLootMultiplier,
            DefaultLootMultiplier,
            "LootMultiplier",
            ref _loggedInvalidLoot);
    }

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

        // 64-bit IL2CPP string layout:
        // 0x00 object header (16 bytes)
        // 0x10 int32 length
        // 0x14 UTF-16 character data
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

        if (*(ushort*)imageBase != 0x5A4D) // MZ
            throw new InvalidOperationException("Loaded GameAssembly.dll has an invalid DOS header.");

        int peOffset = *(int*)(imageBase + 0x3C);
        byte* ntHeaders = imageBase + peOffset;

        if (*(uint*)ntHeaders != 0x00004550) // PE\0\0
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
