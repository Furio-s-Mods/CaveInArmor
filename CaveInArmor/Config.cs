using System.IO;
using Vintagestory.API.Datastructures;

namespace CaveInArmor;
public class CaveInConfig
{
    public int ConfigVersion { get; set; } = 2; 
    public bool Enabled { get; set; } = true;
    public bool EnableDebugLogging { get; set; } = false;
    public bool UseLayered { get; set; } = true;

    public LayeredPieceMultiplier Vertical { get; set; } = new LayeredPieceMultiplier()
    {
        LayeredHeadMultiplier = 1.0f,
        LayeredTorsoMultiplier = 0.5f,
        LayeredLegsMultiplier = 0.1f
    };

    public LayeredPieceMultiplier Horizontal { get; set; } = new LayeredPieceMultiplier()
    {
        LayeredHeadMultiplier = 0.0f,
        LayeredTorsoMultiplier = 0.5f,
        LayeredLegsMultiplier = 1.0f
    };

    public float DurabilityDamageMultiplier { get; set; } = 0.1f;
    public float MinimumDamageThreshold { get; set; } = 0.5f;

    public static CaveInConfig LoadAndValidate(Vintagestory.API.Common.ICoreAPI api, string filename)
    {
        string configPath = Path.Combine(Vintagestory.API.Config.GamePaths.ModConfig, filename);
        string badConfigPath = configPath + ".bad";

        try
        {
            JsonObject rawJson = api.LoadModConfig(filename);

            // CASE 1: file not found
            if (rawJson == null)
            {
                CaveInConfig newConfig = new();
                api.StoreModConfig(newConfig, filename);
                return newConfig;
            }

            // CASE 2: found valid json
            CaveInConfig loadedConfig;
            bool wasUpgraded = false;

            if (!rawJson.KeyExists("ConfigVersion") || rawJson["ConfigVersion"].AsInt() < 2)
            {
                loadedConfig = UpgradeToVersion2(rawJson);
                wasUpgraded = true;
            }
            else
            {
                loadedConfig = rawJson.AsObject<CaveInConfig>();
            }

            bool wasSanitized = loadedConfig.ValidateAndSanitize();

            if (wasUpgraded || wasSanitized)
            {
                api.StoreModConfig(loadedConfig, filename);
                if (wasSanitized)
                {
                    api.Logger.Warning($"[{filename}] Broken config file.");
                }
            }

            return loadedConfig;
        }
        catch (Exception ex)
        {
            // CASE 3: can't read json file
            api.Logger.Error($"=========================================================");
            api.Logger.Error($"[CaveInArmor] CRITICAL: The file {filename} is corrupted and cannot be read!");
            api.Logger.Error($"[CaveInArmor] Error: {ex.Message}");
            api.Logger.Error($"[CaveInArmor] The broken file will be renamed to .bad and a fresh one will be created.");
            api.Logger.Error($"=========================================================");

            try
            {
                if (File.Exists(configPath))
                {
                    if (File.Exists(badConfigPath)) File.Delete(badConfigPath);
                    File.Move(configPath, badConfigPath);
                }
            }
            catch (Exception fileEx)
            {
                api.Logger.Error($"[CaveInArmor] Failed to rename broken config: {fileEx.Message}");
            }

            CaveInConfig fallbackConfig = new();
            api.StoreModConfig(fallbackConfig, filename);
            return fallbackConfig;
        }
    }

    private static CaveInConfig UpgradeToVersion2(JsonObject oldJson)
    {
        CaveInConfig newConfig = new();

        newConfig.Enabled = oldJson["Enabled"].AsBool(newConfig.Enabled);
        newConfig.EnableDebugLogging = oldJson["EnableDebugLogging"].AsBool(newConfig.EnableDebugLogging);
        newConfig.UseLayered = oldJson["UseLayered"].AsBool(newConfig.UseLayered);
        newConfig.DurabilityDamageMultiplier = oldJson["DurabilityDamageMultiplier"].AsFloat(newConfig.DurabilityDamageMultiplier);
        newConfig.MinimumDamageThreshold = oldJson["MinimumDamageThreshold"].AsFloat(newConfig.MinimumDamageThreshold);

        float oldHead = oldJson["LayeredHeadMultiplier"].AsFloat(1.0f);
        float oldTorso = oldJson["LayeredTorsoMultiplier"].AsFloat(0.5f);
        float oldLegs = oldJson["LayeredLegsMultiplier"].AsFloat(0.1f);

        newConfig.Vertical = new LayeredPieceMultiplier() { LayeredHeadMultiplier = oldHead, LayeredTorsoMultiplier = oldTorso, LayeredLegsMultiplier = oldLegs };
        newConfig.Horizontal = new LayeredPieceMultiplier() { LayeredHeadMultiplier = 0.0f, LayeredTorsoMultiplier = 0.5f, LayeredLegsMultiplier = 1.0f };

        return newConfig;
    }

    private bool ValidateAndSanitize()
    {
        bool isCorrupted = false;

        if (Vertical == null) { Vertical = new LayeredPieceMultiplier { LayeredHeadMultiplier = 1.0f, LayeredTorsoMultiplier = 0.5f, LayeredLegsMultiplier = 0.1f }; isCorrupted = true; }
        if (Horizontal == null) { Horizontal = new LayeredPieceMultiplier { LayeredHeadMultiplier = 0.0f, LayeredTorsoMultiplier = 0.5f, LayeredLegsMultiplier = 1.0f }; isCorrupted = true; }

        if (DurabilityDamageMultiplier < 0f) { DurabilityDamageMultiplier = 0.1f; isCorrupted = true; }
        if (MinimumDamageThreshold < 0f)     { MinimumDamageThreshold = 0.5f; isCorrupted = true; }

        if (Vertical.LayeredHeadMultiplier < 0f)  { Vertical.LayeredHeadMultiplier = 1.0f; isCorrupted = true; }
        if (Vertical.LayeredTorsoMultiplier < 0f) { Vertical.LayeredTorsoMultiplier = 0.5f; isCorrupted = true; }
        if (Vertical.LayeredLegsMultiplier < 0f)  { Vertical.LayeredLegsMultiplier = 0.1f; isCorrupted = true; }

        if (Horizontal.LayeredHeadMultiplier < 0f)  { Horizontal.LayeredHeadMultiplier = 0.0f; isCorrupted = true; }
        if (Horizontal.LayeredTorsoMultiplier < 0f) { Horizontal.LayeredTorsoMultiplier = 0.5f; isCorrupted = true; }
        if (Horizontal.LayeredLegsMultiplier < 0f)  { Horizontal.LayeredLegsMultiplier = 1.0f; isCorrupted = true; }

        return isCorrupted;
    }
}

public class LayeredPieceMultiplier
{
    public float LayeredHeadMultiplier { get; set; }
    public float LayeredTorsoMultiplier { get; set; }
    public float LayeredLegsMultiplier { get; set; }
}
