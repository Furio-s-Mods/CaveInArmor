global using System;
using System.Reflection;
using HarmonyLib;
using MyModdingTools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CaveInArmor;

public class CaveInArmorSystem : ModSystem
{
    private Harmony harmony;
    private const string ModName = "caveinarmor";
    private const string HarmonyId = $"com.furio.{ModName}";
    private const string TargetClassName = "Vintagestory.GameContent.ModSystemWearableStats";
    private const string TargetMethodName = "handleDamaged";

    public CaveInConfig Config { get; private set; }
    public ICoreServerAPI ServerApi { get; private set; }
    public CustomLogger CustomLogger { get; private set; }
    public static CaveInArmorSystem Instance { get; private set; }
    private bool disposed;

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        Instance = this;
        ServerApi = api;
        
        Config = api.LoadModConfig<CaveInConfig>($"{ModName}Config.json");
        CustomLogger = new CustomLogger(api.Logger, ModName, Config?.EnableDebugLogging == true);

        try
        {
            if (Config == null)
            {
                Config = CaveInConfig.CreateDefaultConfig();
                api.StoreModConfig(Config, $"{ModName}Config.json");
                CustomLogger.Notification("Generated fresh fallback configuration file.");
            }
        }
        catch (Exception ex)
        {
            Config = CaveInConfig.CreateDefaultConfig();
            CustomLogger.Error($"Failed parsing config file, loading default parameters. Error: {ex.Message}");
        }

        if (!Config.Enabled) return;

        harmony = new Harmony(HarmonyId);

        try
        {
            Type targetType = typeof(ModSystemWearableStats);
            MethodInfo originalMethod = AccessTools.Method(targetType, TargetMethodName);

            if (originalMethod == null)
            {
                CustomLogger.Error($"Critical targeting failure. Target method '{TargetMethodName}' not found!");
                return;
            }

            MethodInfo prefixMethod = AccessTools.Method(typeof(WearableStatsPatch), nameof(WearableStatsPatch.Prefix));
            harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
            CustomLogger.Notification("Successfully patched cave-in defense calculations.");
        }
        catch (Exception ex)
        {
            CustomLogger.Error($"Failed applying Harmony initialization sequence: {ex.Message}");
        }
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;

        harmony?.UnpatchAll(HarmonyId);
        harmony = null;

        ServerApi = null;
        CustomLogger = null;

        Instance = null;
        base.Dispose();
    }
}
