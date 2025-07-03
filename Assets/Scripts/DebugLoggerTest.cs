using UnityEngine;

/// <summary>
/// Test script to demonstrate the DebugLogger functionality.
/// This script can be attached to a GameObject to test the logging system.
/// </summary>
public class DebugLoggerTest : MonoBehaviour
{
    [Header("Test Controls")]
    [SerializeField] private bool runTestOnStart = false;
    [SerializeField] private KeyCode testKey = KeyCode.L;

    private void Start()
    {
        if (runTestOnStart)
        {
            RunLoggingTest();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            RunLoggingTest();
        }
    }

    /// <summary>
    /// Run a comprehensive test of the DebugLogger system
    /// </summary>
    public void RunLoggingTest()
    {
        Debug.Log("=== DebugLogger Test Started ===");
        
        // Test verbose logging status
        bool isVerboseEnabled = DebugLogger.IsVerboseEnabled();
        DebugLogger.LogImportant($"Verbose logging is currently: {(isVerboseEnabled ? "ENABLED" : "DISABLED")}");
        
#if UNITY_EDITOR
        DebugLogger.LogImportant("Running in Unity Editor - verbose logs should always be enabled");
#else
        DebugLogger.LogImportant("Running in build - verbose logs controlled by config.json");
#endif

        // Test different log levels
        DebugLogger.LogImportant("This is an IMPORTANT log - should always be visible");
        DebugLogger.LogVerbose("This is a VERBOSE log - visibility depends on settings");
        DebugLogger.LogWarning("This is a WARNING log - should always be visible");
        DebugLogger.LogError("This is an ERROR log - should always be visible");

        // Test configuration status
        var config = ServerConfig.Instance;
        if (config != null)
        {
            bool configVerbose = config.logging?.enableVerboseLogs ?? false;
            DebugLogger.LogImportant($"Config verbose setting: {configVerbose}");
        }
        else
        {
            DebugLogger.LogWarning("ServerConfig.Instance is null - config not loaded yet");
        }

        Debug.Log("=== DebugLogger Test Completed ===");
    }

    /// <summary>
    /// Test method that can be called from UI buttons
    /// </summary>
    public void TestVerboseLogging()
    {
        DebugLogger.LogVerbose("Test verbose message from UI button");
    }

    /// <summary>
    /// Test method to refresh logger settings
    /// </summary>
    public void RefreshLoggerSettings()
    {
        DebugLogger.RefreshSettings();
        DebugLogger.LogImportant("DebugLogger settings refreshed");
        RunLoggingTest();
    }
}
