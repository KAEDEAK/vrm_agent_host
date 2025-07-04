using UnityEngine;

/// <summary>
/// Centralized logging system with configurable verbose logging.
/// In Unity Editor, verbose logs are always enabled regardless of configuration.
/// In builds, verbose logs can be controlled via config.json.
/// </summary>
public static class DebugLogger 
{
    private static bool? _verboseLogsEnabled = null;
    
    private static bool VerboseLogsEnabled 
    {
        get 
        {
            if (_verboseLogsEnabled == null) 
            {
                RefreshSettings();
            }
            return _verboseLogsEnabled.Value;
        }
    }
    
    /// <summary>
    /// Refresh logging settings from ServerConfig.
    /// Called automatically when needed, but can be called manually after config changes.
    /// </summary>
    public static void RefreshSettings() 
    {
#if UNITY_EDITOR
        // Always enable verbose logs in Unity Editor
        _verboseLogsEnabled = true;
#else
        var config = ServerConfig.Instance;
        _verboseLogsEnabled = config?.logging?.enableVerboseLogs ?? false;
#endif
    }
    
    /// <summary>
    /// Log verbose/detailed information that can be disabled in production.
    /// Always shown in Unity Editor, controlled by config in builds.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void LogVerbose(string message) 
    {
        if (VerboseLogsEnabled) 
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// Log important information that should always be shown.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void LogImportant(string message) 
    {
        Debug.Log(message);
    }
    
    /// <summary>
    /// Log error messages (always shown).
    /// </summary>
    /// <param name="message">The error message to log</param>
    public static void LogError(string message) 
    {
        Debug.LogError(message);
    }
    
    /// <summary>
    /// Log warning messages (always shown).
    /// </summary>
    /// <param name="message">The warning message to log</param>
    public static void LogWarning(string message) 
    {
        Debug.LogWarning(message);
    }
    
    /// <summary>
    /// Check if verbose logging is currently enabled.
    /// </summary>
    /// <returns>True if verbose logging is enabled</returns>
    public static bool IsVerboseEnabled() 
    {
        return VerboseLogsEnabled;
    }
}
