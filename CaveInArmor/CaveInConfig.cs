namespace CaveInArmor;

public class CaveInConfig
{
    public bool Enabled { get; set; }
    public bool EnableDebugLogging { get; set; }
    public bool UseLayered { get; set; }
    public float LayeredHeadMultiplier { get; set; }
    public float LayeredTorsoMultiplier { get; set; }
    public float LayeredLegsMultiplier { get; set; }
    public float DurabilityDamageMultiplier { get; set; }
    public float MinimumDamageThreshold { get; set; }

    public static CaveInConfig CreateDefaultConfig()
    {
        return new CaveInConfig
        {
            Enabled = true,
            EnableDebugLogging = false,
            UseLayered = false,
            LayeredHeadMultiplier = 1.0f,
            LayeredTorsoMultiplier = 0.5f,
            LayeredLegsMultiplier = 0.1f,
            DurabilityDamageMultiplier = 0.1f,
            MinimumDamageThreshold = 0.5f
        };
    }
}