global using System;
using HarmonyLib;
using MyModdingTools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CaveInArmor;

public class CaveInArmorSystem : ModSystem
{
    private Harmony harmony;
    private const string ModName = "caveinarmor";
    private const string ConfigFileName = $"{ModName}Config.json";
    private const string HarmonyId = $"com.furio.{ModName}";

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
        Config = CaveInConfig.LoadAndValidate(api, ConfigFileName);
        CustomLogger = new CustomLogger(api.Logger, ModName, Config?.EnableDebugLogging == true);

        if (!Config.Enabled) return;


        try
        {
            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
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
