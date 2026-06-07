using Vintagestory.API.Common;

namespace MyModdingTools;

/// <summary>
/// Initializes a new log manager for a specific mod.
/// </summary>
/// <param name="gameLogger">The logger provided by the Vintage Story API (api.Logger).</param>
/// <param name="modName">The name or ID of the mod to be used as a prefix.</param>
/// <param name="writeLog">A flag to toggle non critical mod logging on or off (useful for testing builds without rebuilding).</param>
public class CustomLogger(ILogger gameLogger, string modName, bool writeLog = true)
{
    private readonly ILogger gameLogger = gameLogger;
    private readonly string modName = modName;
    private readonly bool writeLog = writeLog;

    public void WriteLog(EnumLogType logType, string message)
    {
        if (gameLogger == null) return;
        
        if (writeLog)
        {
            gameLogger.Log(logType, $"[{modName}] {message}");
        }
    }

    public void Notification(string message) => WriteLog(EnumLogType.Notification, message);
    public void Warning(string message) => WriteLog(EnumLogType.Warning, message);
    public void Error(string message) => WriteLog(EnumLogType.Error, message);
    public void Fatal(string message) => WriteLog(EnumLogType.Fatal, message);
}
