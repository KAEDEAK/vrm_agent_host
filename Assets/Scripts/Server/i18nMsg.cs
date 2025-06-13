using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


public static class i18nMsg {
    [System.Serializable]
    public class LocalizationData {
        public string default_lang;
        public LanguageEntry[] en;
        public LanguageEntry[] ja;
    }

    [System.Serializable]
    public class LanguageEntry {
        public string key;
        public string value;
    }

    private static Dictionary<string, string> localizedText = new Dictionary<string, string>();

    private static readonly Dictionary<string, string> defaultMessages = new Dictionary<string, string>
    {   
        /* AnimationServer.cs */
        { "ANIMSERVER_FIXED_FRAMERATE", "[AnimationServer] Fixed frame rate: {0}" },
        { "ANIMSERVER_CONFIG_NOT_FOUND", "[AnimationServer] config.json not found: {0}" },
        { "ANIMSERVER_CONFIG_LOAD_ERROR", "[AnimationServer] Error reading config.json: {0}" },
        { "ANIMSERVER_START_SUCCESS", "[AnimationServer] HTTP server started. Port: {0}" },
        { "ANIMSERVER_START_ERROR", "[AnimationServer] Server start error: {0}" },
        { "ANIMSERVER_STOP_ERROR", "[AnimationServer] Error stopping server: {0}" },
        { "ANIMSERVER_REQUEST_ERROR", "[AnimationServer] Request reception error: {0}" },
        { "ANIMSERVER_REQUEST_RECEIVED", "[AnimationServer] Request: {0}" },
        { "VRM_FILE_NOT_FOUND", "VRM file not found: {0}" },
        { "BACKGROUND_FILE_NOT_FOUND", "Background image file not found: {0}" },
        { "INVALID_TARGET", "Invalid target: {0}" },
        { "ANIMSERVER_RESPONSE_SEND_ERROR", "[AnimationServer] Error sending immediate response: {0}" },
        { "RESPONSE_SEAMLESS_BLINK_RESET_STARTED", "Seamless blink reset started." },
        { "RESPONSE_BLINK_RESET_ZERO", "Blink reset to 0." },
        { "RESPONSE_SEAMLESS_BLINK_SET", "Seamless blink set to {0}" },
        { "RESPONSE_BLINK_APPLIED", "Blink applied => {0}" },
        { "ERROR_INVALID_BLINK_PARAM", "Invalid blink param => {0}, error={1}" },
        { "RESPONSE_SEAMLESS_BLENDSHAPE_APPLIED_ALT", "Seamless blendshape applied => {0}" },
        { "RESPONSE_BLENDSHAPE_APPLIED_ALT", "Blendshape applied => {0}" },
        { "ERROR_INVALID_BLENDSHAPE_NAME_ALT", "Invalid blendshape name => {0}" },
        { "RESPONSE_SEAMLESS_MOUTH_RESET_STARTED_ALT", "[Mouth] seamless reset blendshapes started." },
        { "RESPONSE_MOUTH_ALL_RESET_ALT", "[Mouth] all blendshapes reset." },
        { "ERROR_UNSUPPORTED_MOUTH_SHAPE_ALT", "Unsupported mouth shape => {0}" },
        { "RESPONSE_SEAMLESS_MOUTH_ANIMATION_ALT", "Seamless mouth animation => {0}" },
        { "RESPONSE_MOUTH_ANIMATION_ALT", "Mouth animation => {0}" },
        { "ERROR_ENABLE_PARAM_MISSING", "ERROR: Missing 'enable' parameter." },
        { "ERROR_ENABLE_PARAM_INVALID", "ERROR: Invalid 'enable' parameter. Use 'true' or 'false'." },
        { "RESPONSE_AUTO_PREPARE_SEAMLESS_SET", "autoPrepareSeamless set to {0}" },
        { "RESPONSE_AUTO_PREPARE_SEAMLESS_STATUS", "autoPrepareSeamless: {0}" },
        { "RESPONSE_AUTOBLINK_STATUS", "autoBlink => enabled={0}, freq(ms)={1}" },
        { "RESPONSE_SEAMLESS_BLINK_RESET_CMD", "Seamless blink reset started. (cmd=reset_blink)" },
        { "RESPONSE_BLINK_RESET_DONE_CMD", "Blink reset done. (cmd=reset_blink)" },
        { "RESPONSE_SEAMLESS_MOUTH_RESET_CMD", "[Mouth] seamless reset blendshapes started. (cmd=reset_mouth)" },
        { "RESPONSE_MOUTH_ALL_RESET_CMD", "[Mouth] all blendshapes reset. (cmd=reset_mouth)" },

        /* AnimationHandler.cs */
        { "LOG_ANIMATION_STOPPED", "Animation stopped." },
        { "ERROR_ANIMATOR_CONTROLLER_NOT_SET", "Animator or AnimatorController is not set." },
        { "LOG_VRM_MODEL_LOADED", "VRM model loaded. Applying AnimatorController." },
        { "LOG_ANIMATIONHANDLER_INITIALIZED", "AnimationHandler initialization complete." },
        { "ERROR_CONTROLLER_NOT_SET", "AnimatorController is not set in the Inspector." },
        { "LOG_CONTROLLER_APPLIED", "AnimatorController applied immediately." },
        { "WARNING_NOT_INITIALIZED", "AnimationHandler is not yet initialized." },
        { "LOG_PLAY_INITIAL_ANIMATION", "Playing initial animation." },
        { "ERROR_VRMLOADER_NOT_ATTACHED", "VRMLoader is not attached." },
        { "WARNING_NOT_INITIALIZED_SKIP", "AnimationHandler is not yet initialized. Skipping animation." },
        { "ERROR_ANIMATOR_NOT_SET", "Animator is not set." },
        { "ERROR_INVALID_ANIMATION", "Invalid animation: {0}" },
        { "ERROR_INVALID_ANIMATION_ID", "Invalid animation ID: {0}" },
        { "ERROR_ANIMATION_NAME_NOT_FOUND", "Animation name for ID:{0} not found." },
        { "LOG_SEAMLESS_TRANSITION", "Seamless transition: Changing to state '{0}' (Layer {1}) in {2} seconds." },
        { "LOG_FORCE_STATE", "Forcing state '{0}' (Layer {1}) to maintain state." },
        { "LOG_ANIMATION_PLAYED", "Animation '{0}' played (after Animator configuration)." },
        { "ERROR_VRM_MODEL_NOT_LOADED", "VRM model is not loaded." },
        { "LOG_PREVIOUS_VRMA_DESTROYED", "Previous VRMA instance destroyed." },
        { "ERROR_VRMA_NOT_FOUND", ".vrma file not found: {0}" },
        { "ERROR_VRMA_LOAD_FAILED", "Failed to load .vrma animation: {0}" },
        { "LOG_VRMA_APPLIED", ".vrma animation '{0}' applied (Loop: {1})." },
        { "WARNING_VRM_RUNTIME_NOT_FOUND", "⚠ Vrm10Runtime not found." },
        { "LOG_ANIMATION_PLAY_STARTED", "Animation playback started!" },
        { "LOG_VRMA_ANIMATION_RESET", "VRMA animation reset." },
        { "LOG_AGIA_RESET", "AGIA AnimatorController reset and initial animation played." },
        { "ERROR_VRMA_BINARY_READ_FAILED", "Failed to read VRMA file binary data." },
        { "LOG_VRMA_BINARY_LOAD_SUCCESS", "✅ VRMA binary data loaded successfully: {0} bytes" },
        { "ERROR_VRMA_INSTANCE_LOAD_FAILED", "Failed to load VRMA instance." },
        { "LOG_VRMA_LOADING_START", "[ImportVrmaAnimation] Starting VRMA load: {0}" },
        { "ERROR_GLTFS_PARSE_FAILED", "[ImportVrmaAnimation] Failed to parse GLTF data." },
        { "LOG_VRMA_IMPORTER_CREATED", "[ImportVrmaAnimation] VrmAnimationImporter created: {0}" },
        { "ERROR_MATERIAL_NULL", "[ImportVrmaAnimation] Default material is null in GetDefaultMaterialAsync()!" },
        { "ERROR_SHADER_NULL", "[ImportVrmaAnimation] Shader is null in GetDefaultMaterialAsync()!" },
        { "LOG_DEFAULT_MATERIAL", "✅ Default material: {0}, Shader: {1}" },
        { "ERROR_VRMA_INSTANCE_COMPONENT_MISSING", "[ImportVrmaAnimation] Vrm10AnimationInstance component missing." },
        { "LOG_VRMA_INSTANCE_SUCCESS", "[ImportVrmaAnimation] Vrm10AnimationInstance acquired successfully: {0}" },
        { "LOG_MATERIAL_LOADED", "✅ Loaded material: {0}, Shader: {1}" },
        { "ERROR_IMPORT_VRMA_EXCEPTION", "[ImportVrmaAnimation] Exception: {0}\n{1}" },
        { "WARNING_VRMA_INSTANCE_MISSING", "VRMA instance is missing." },
        { "LOG_SKINNEDMESH_DISABLED", "Disabled SkinnedMeshRenderer: {0}" },
        { "WARNING_ANIMATION_COMPONENT_MISSING", "Animation component is missing. Playback cannot be executed." },
        /* AnimationCommandHandler.cs */
        { "RESPONSE_ANIMATION_SYSTEM_NOT_INITIALIZED", "Animation system is not initialized." },
        { "RESPONSE_AGIA_RESET", "AGIA AnimatorController has been reset and the initial animation has been played." },
        { "RESPONSE_VRMA_ANIMATION_STARTED", ".vrma animation started: {0} (loop: {1})" },
        { "RESPONSE_ANIMATION_ID_NOT_SPECIFIED", "Animation id is not specified." },
        { "RESPONSE_INVALID_ANIMATION_ID", "Invalid animation ID: {0}" },
        { "RESPONSE_SEAMLESS_ANIMATION_STARTED", "Animation started with seamless transition." },
        { "RESPONSE_ANIMATION_STARTED", "Animation started." },
        { "RESPONSE_ANIMATION_STOP", "Animation stopped." },
        { "RESPONSE_SHAPE_PARAM_REQUIRED", "Shape command requires a shape parameter." },
        { "ERROR_VRMLOADER_NOT_FOUND", "VRMLoader not found." },
        { "ERROR_EXPRESSION_SYSTEM_NOT_AVAILABLE", "Expression system is not available." },
        { "RESPONSE_SEAMLESS_RESET_BLENDSHAPES_STARTED", "Seamless reset of all blendshapes started." },
        { "RESPONSE_ALL_BLENDSHAPES_RESET", "All blendshapes reset successfully." },
        { "RESPONSE_SEAMLESS_BLENDSHAPE_APPLIED", "Seamless blendshape '{0}' applied successfully." },
        { "RESPONSE_BLENDSHAPE_APPLIED", "Blendshape '{0}' applied successfully." },
        { "ERROR_INVALID_BLENDSHAPE_NAME", "Invalid blendshape name: '{0}'." },
        { "RESPONSE_MOUTH_PARAM_REQUIRED", "Mouth command requires a word parameter." },
        { "ERROR_UNSUPPORTED_MOUTH_SHAPE", "Unsupported mouth shape: {0}" },
        { "RESPONSE_SEAMLESS_MOUTH_ANIMATION_STARTED", "Seamless mouth animation for '{0}' started." },
        { "RESPONSE_MOUTH_ANIMATION_STARTED", "Mouth animation for '{0}' started." },
        { "ERROR_INVALID_ANIMATION_COMMAND", "Invalid animation command: {0}" },
        /* AudioLipSync.cs */
        { "AUDIOSYNC_ALREADY_ACTIVE", "Lip sync is already active. Stopping current session." },
        { "AUDIOSYNC_INVALID_CHANNEL", "Invalid channel ID: {0}" },
        { "AUDIOSYNC_MIC_STARTED", "Microphone lip sync started." },
        { "AUDIOSYNC_MIC_NOT_FOUND", "No microphone devices found." },
        { "AUDIOSYNC_WASAPI_STARTED", "WASAPI lip sync started." },
        { "AUDIOSYNC_WASAPI_START_FAILED", "Failed to start WASAPI lip sync: {0}" },
        { "AUDIOSYNC_STOPPED", "Lip sync stopped." },
        { "AUDIOSYNC_ON_RESPONSE", "Lip sync started on channel {0}." },
        { "AUDIOSYNC_OFF_RESPONSE", "Lip sync turned off." },
        { "AUDIOSYNC_INVALID_CMD", "Invalid lip sync command: {0}" },
        { "AUDIOSYNC_RESPONSE_SEND_ERROR", "Error sending lip sync response: {0}" },
        /* VRMLoader.cs */
        { "VRML_RELOADING_IGNORED", "VRM is currently reloading. New requests are ignored." },
        { "VRML_EXISTING_MODEL_DESTROYED", "Destroying existing VRM model." },
        { "VRML_LOAD_START", "Starting VRM load. Path: {0}" },
        { "VRML_LOAD_FAILED", "Failed to load VRM." },
        { "VRML_LOAD_SUCCESS", "VRM model loaded successfully." },
        { "VRML_LOAD_EXCEPTION", "An exception occurred during VRM load: {0}" },
        { "VRML_UPDATE_WHEN_OFFSCREEN_ENABLED", "Update When Offscreen enabled for: {0}" },
        { "VRML_MAIN_CAMERA_NOT_FOUND", "Main camera not found." },
        { "VRML_HEAD_BONE_NOT_FOUND", "Head bone not found." },
        { "VRML_CAMERA_ADJUSTED", "Camera adjustment complete. Position: {0}, Rotation: {1}" },
        /* ShadowConfigurator.cs */
        { "ERROR_CONFIG_FILE_NOT_FOUND", "Config file not found: {0}" },
        { "ERROR_CONFIG_LOAD_FAILED", "Failed to load config!" },
        { "ERROR_DIRECTIONAL_LIGHT_NOT_FOUND", "Directional Light not found!" },
        { "LOG_SHADOW_RESOLUTION_SET", "Shadow Resolution set: {0}" },
        { "ERROR_MAIN_CAMERA_NOT_FOUND", "Main Camera not found!" },
        { "LOG_ANTIALIASING_SET", "Anti-aliasing set: {0}x" },
        { "WARNING_DIRECTIONAL_LIGHT_CONFIG_NOT_FOUND", "Directional Light or its configuration not found." },
        { "LOG_DIRECTIONAL_LIGHT_ROTATION_SET", "Applied Directional Light rotation: {0}" },
        { "LOG_DIRECTIONAL_LIGHT_RENDERING_SET", "Applied Directional Light Rendering settings: RenderingLayerMask={0}, CullingMask={1}" },
        { "ERROR_VRM_MODEL_NULL", "VRM model is null!" },
        { "LOG_SHADOW_SETTINGS_APPLIED", "Applied shadow settings: {0}" },
        { "WARNING_MATERIAL_CONFIG_MISSING", "Material configuration not found in config.json. Applying default values." },
        { "LOG_MATERIAL_SHADOW_RECEIVE_SET", "Applied material shadow receive settings: {0}" },
        { "LOG_MATERIAL_SHADOW_GRADE_SET", "Applied material shadow grade settings: {0}" },
        { "LOG_SHADING_TOONY_SET", "Shading Toony set: {0} ({1})" },
        { "LOG_SHADING_SHIFT_SET", "Shading Shift set: {0} ({1})" },
        { "LOG_RIM_COLOR_SET", "Rim Color set: {0} ({1})" },
        { "LOG_RIM_LIGHTING_MIX_SET", "Rim Lighting Mix set: {0} ({1})" },
        { "LOG_RIM_FRESNEL_POWER_SET", "Rim Fresnel Power set: {0} ({1})" },
        { "LOG_RIM_LIFT_SET", "Rim Lift set: {0} ({1})" },
        { "LOG_OUTLINE_WIDTH_SET", "Outline Width set: {0} ({1})" },
        { "LOG_OUTLINE_COLOR_SET", "Outline Color set: {0} ({1})" },
        { "LOG_OUTLINE_LIGHTING_MIX_SET", "Outline Lighting Mix set: {0} ({1})" },
        { "WARNING_COLOR_PARSE_FAILED", "Failed to parse color: {0}" },
        { "ERROR_DIRECTIONAL_LIGHT_NOT_ASSIGNED", "Directional Light is not assigned. Please set it in the Inspector." },
        { "ERROR_HEAD_BONE_NOT_FOUND", "Head bone not found." },
        { "LOG_DIRECTIONAL_LIGHT_POSITION_ADJUSTED", "Directional Light position adjusted: {0}, rotation: {1}" },
        /* LocalImageLoader.cs */
        { "ERROR_CANVAS_OR_IMAGE_NOT_SET", "Background Canvas or Image is not set. Please configure in the Inspector." },
        { "LOG_CANVAS_RENDERMODE_SET", "Background Canvas RenderMode set to Screen Space - Camera." },
        { "ERROR_IMAGE_FILE_NOT_FOUND", "Image file not found: {0}. Background switching and initialization aborted." },
        { "ERROR_IMAGE_LOAD_FAILED", "Failed to load image: {0}. Background switching and initialization aborted." },
        { "WARNING_IMAGE_OR_SPRITE_NOT_SET", "Background Image or Sprite not set. Skipping size adjustment." },
        { "ERROR_RECTTRANSFORM_NOT_FOUND", "RectTransform not found." },
        { "WARNING_BACKGROUND_IMAGE_NOT_SET", "Background image not set. Skipping Canvas position adjustment." },
        { "LOG_CANVAS_POSITION_ADJUSTED", "Background Canvas position adjusted: {0}" },
        { "LOG_VRM_MODEL_LOADED_BACKGROUND", "VRM model loaded." },
        { "LOG_BACKGROUND_IMAGE_VALID", "Background image is valid, adjusting Canvas position." },
        /* BackgroundCommandHandler.cs */
        { "RESPONSE_FILE_PARAM_MISSING", "Missing 'file' parameter." },
        { "ERROR_IMAGE_LOAD_FAILURE", "Image file does not exist or failed to load: {0}" },
        { "RESPONSE_BACKGROUND_IMAGE_CHANGED", "Background image changed to {0}." },
        { "ERROR_INVALID_BACKGROUND_CMD", "Invalid background command: {0}" },
        /* HttpCommandHandlerBase.cs */
        { "ERROR_RESPONSE_SEND", "[HttpCommandHandlerBase] Response sending error: {0}" },
        /* LipSyncCommandHandler.cs */
        { "RESPONSE_LIPSYNC_ON", "LipSync started on channel {0}!" },
        { "RESPONSE_LIPSYNC_OFF", "LipSync stopped!" },
        { "ERROR_INVALID_LIPSYNC_CMD", "Invalid lip sync command: {0}" },
        /* ServerCommandHandler.cs */
        { "RESPONSE_SERVER_TERMINATE", "Server is shutting down." },
        { "ERROR_INVALID_SERVER_CMD", "Invalid server command: {0}" },
        { "LOG_SERVER_SHUTDOWN_INITIATED", "[ServerCommandHandler] Server shutdown initiated." },
        { "ERROR_UNKNOWN_COMMAND", "Unknown command received. Please check your request." },
        /* VrmCommandHandler.cs */
        { "ERROR_VRM_FILE_NOT_FOUND", "VRM file not found: {0}" },
        { "RESPONSE_VRM_LOADED", "VRM loaded: {0}" },
        { "LOG_VRM_LOAD_REQUEST", "[VrmCommandHandler] VRM load requested. File path: {0}" },
        { "ERROR_INVALID_VRM_CMD", "Invalid VRM command: {0}" },
        { "ERROR_VRM_NOT_LOADED", "VRM model is not loaded." },
        { "ERROR_PARAM_XYZ_MISSING", "Missing xyz parameter." },
        { "ERROR_PARAM_XYZ_INVALID_FORMAT", "Invalid format for xyz parameter." },
        { "RESPONSE_CURRENT_POSITION", "Current position: {0}" },
        { "RESPONSE_CURRENT_ROTATION", "Current rotation: {0}" },
        { "RESPONSE_ROTATION_SET", "Rotation set to {0}." },

        { "ERROR_DEPRECATED_COMMAND", "The command '{0}' is deprecated and no longer supported." },
        { "INFO_ROTATION_LIMITED_BY_LICENSE", "X and Z axis rotation were limited to ±20 degrees due to license restrictions (sexual expression not allowed)."},

    };

    static i18nMsg() {
        localizedText = new Dictionary<string, string>(defaultMessages); // ← 安全なフォールバック
        /*
        AnimationServer.csのawake()
        try {

            LoadLocalization();
        }
        catch (Exception ex) {
            Debug.LogError($"[i18nMsg] Failed to initialize localization: {ex.Message}");
            localizedText = new Dictionary<string, string>(defaultMessages); // ← 安全なフォールバック
        }
        */
    }
    public static void InitializeLocalization() {
        try {
            LoadLocalization();
        }
        catch (Exception ex) {
            Debug.LogError($"[i18nMsg] Localization init failed: {ex.Message}");
        }
    }


    private static void LoadLocalization() {
        string filePath = UserPaths.LocalizationPath;

        if (!File.Exists(filePath)) {
            Debug.LogWarning("[Localization] File not found. Using defaults.");
            return;
        }

        try {
            string json = File.ReadAllText(filePath);

            if (!IsValidJson(json)) {
                Debug.LogWarning("[Localization] JSON invalid. Using defaults.");
                return;
            }

            if (!TryParseJson(json, out var parsed)) {
                Debug.LogError("[Localization] Invalid JSON. Using default messages.");
                localizedText = new Dictionary<string, string>(defaultMessages);
                return;
            }


            var data = JsonUtility.FromJson<LocalizationData>(json);

            if (data == null || data.en == null) {
                Debug.LogWarning("[Localization] JSON invalid. Using defaults.");
                return;
            }

            string lang = PlayerPrefs.GetString("selected_language", data.default_lang);
            var entries = lang == "ja" ? data.ja : data.en;

            localizedText = new Dictionary<string, string>();
            foreach (var e in entries) {
                if (!string.IsNullOrWhiteSpace(e.key))
                    localizedText[e.key] = e.value;
            }
        }
        catch (Exception ex) {
            Debug.LogError($"[Localization] Error parsing JSON: {ex.Message}");
        }
    }
    private static bool TryParseJson(string json, out LocalizationData data) {
        data = null;
        try {
            data = JsonUtility.FromJson<LocalizationData>(json);
            return data != null;
        }
        catch (Exception ex) {
            Debug.LogError($"[TryParseJson] Failed: {ex.Message}");
            return false;
        }
    }

    public static bool IsValidJson(string json) {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        json = json.Trim();

        if (!(json.StartsWith("{") && json.EndsWith("}")) &&
            !(json.StartsWith("[") && json.EndsWith("]")))
            return false;

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = 0; i < json.Length; i++) {
            char c = json[i];

            if (escape) {
                escape = false;
                continue;
            }

            if (c == '\\') {
                escape = true;
                continue;
            }

            if (c == '\"') {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{' || c == '[')
                depth++;
            else if (c == '}' || c == ']')
                depth--;
        }

        return depth == 0 && !inString;
    }



    /*
    private static void LoadLocalization()
    {
        string filePath = UserPaths.LocalizationPath;

        if (File.Exists(filePath))
        {
            string dataAsJson = File.ReadAllText(filePath);
            var json = JsonUtility.FromJson<LocalizationData>(dataAsJson);

                Debug.Log($"JSON raw: {dataAsJson}");

                if (json.resourceStrings == null)
                {
                    Debug.LogError("json.resourceStrings is null!");
                }
                else if (json.resourceStrings.resourceStrings == null)
                {
                    Debug.LogError("json.resourceStrings.resourceStrings is null!");
                }

            string lang = json.resourceStrings.default_lang;

            if (json.resourceStrings.resourceStrings.ContainsKey(PlayerPrefs.GetString("selected_language", lang)))
            {
                lang = PlayerPrefs.GetString("selected_language", lang);
            }

            foreach (var item in json.resourceStrings.resourceStrings[lang])
            {
                localizedText[item.Key] = item.Value;
            }
        }
        else
        {
            Debug.LogWarning("Localization file not found. Using default messages.");
            localizedText = new Dictionary<string, string>(defaultMessages);
        }
    }
    */

    public static string ANIMSERVER_FIXED_FRAMERATE => GetLocalizedText("ANIMSERVER_FIXED_FRAMERATE", "[AnimationServer] Fixed frame rate: {0}");
    public static string ANIMSERVER_CONFIG_NOT_FOUND => GetLocalizedText("ANIMSERVER_CONFIG_NOT_FOUND", "[AnimationServer] config.json not found: {0}");
    public static string ANIMSERVER_CONFIG_LOAD_ERROR => GetLocalizedText("ANIMSERVER_CONFIG_LOAD_ERROR", "[AnimationServer] Error reading config.json: {0}");
    public static string ANIMSERVER_START_SUCCESS => GetLocalizedText("ANIMSERVER_START_SUCCESS", "[AnimationServer] HTTP server started. Port: {0}");
    public static string ANIMSERVER_START_ERROR => GetLocalizedText("ANIMSERVER_START_ERROR", "[AnimationServer] Server start error: {0}");
    public static string ANIMSERVER_STOP_ERROR => GetLocalizedText("ANIMSERVER_STOP_ERROR", "[AnimationServer] Error stopping server: {0}");
    public static string ANIMSERVER_REQUEST_ERROR => GetLocalizedText("ANIMSERVER_REQUEST_ERROR", "[AnimationServer] Request reception error: {0}");
    public static string ANIMSERVER_REQUEST_RECEIVED => GetLocalizedText("ANIMSERVER_REQUEST_RECEIVED", "[AnimationServer] Request: {0}");
    public static string VRM_FILE_NOT_FOUND => GetLocalizedText("VRM_FILE_NOT_FOUND", "VRM file not found: {0}");
    public static string BACKGROUND_FILE_NOT_FOUND => GetLocalizedText("BACKGROUND_FILE_NOT_FOUND", "Background image file not found: {0}");
    public static string INVALID_TARGET => GetLocalizedText("INVALID_TARGET", "Invalid target: {0}");
    public static string ANIMSERVER_RESPONSE_SEND_ERROR => GetLocalizedText("ANIMSERVER_RESPONSE_SEND_ERROR", "[AnimationServer] Error sending immediate response: {0}");
    public static string RESPONSE_SEAMLESS_BLINK_RESET_STARTED => GetLocalizedText("RESPONSE_SEAMLESS_BLINK_RESET_STARTED", "Seamless blink reset started.");
    public static string RESPONSE_BLINK_RESET_ZERO => GetLocalizedText("RESPONSE_BLINK_RESET_ZERO", "Blink reset to 0.");
    public static string RESPONSE_SEAMLESS_BLINK_SET => GetLocalizedText("RESPONSE_SEAMLESS_BLINK_SET", "Seamless blink set to {0}");
    public static string RESPONSE_BLINK_APPLIED => GetLocalizedText("RESPONSE_BLINK_APPLIED", "Blink applied => {0}");
    public static string ERROR_INVALID_BLINK_PARAM => GetLocalizedText("ERROR_INVALID_BLINK_PARAM", "Invalid blink param => {0}, error={1}");
    public static string RESPONSE_SEAMLESS_BLENDSHAPE_APPLIED_ALT => GetLocalizedText("RESPONSE_SEAMLESS_BLENDSHAPE_APPLIED_ALT", "Seamless blendshape applied => {0}");
    public static string RESPONSE_BLENDSHAPE_APPLIED_ALT => GetLocalizedText("RESPONSE_BLENDSHAPE_APPLIED_ALT", "Blendshape applied => {0}");
    public static string ERROR_INVALID_BLENDSHAPE_NAME_ALT => GetLocalizedText("ERROR_INVALID_BLENDSHAPE_NAME_ALT", "Invalid blendshape name => {0}");
    public static string RESPONSE_SEAMLESS_MOUTH_RESET_STARTED_ALT => GetLocalizedText("RESPONSE_SEAMLESS_MOUTH_RESET_STARTED_ALT", "[Mouth] seamless reset blendshapes started.");
    public static string RESPONSE_MOUTH_ALL_RESET_ALT => GetLocalizedText("RESPONSE_MOUTH_ALL_RESET_ALT", "[Mouth] all blendshapes reset.");
    public static string ERROR_UNSUPPORTED_MOUTH_SHAPE_ALT => GetLocalizedText("ERROR_UNSUPPORTED_MOUTH_SHAPE_ALT", "Unsupported mouth shape => {0}");
    public static string RESPONSE_SEAMLESS_MOUTH_ANIMATION_ALT => GetLocalizedText("RESPONSE_SEAMLESS_MOUTH_ANIMATION_ALT", "Seamless mouth animation => {0}");
    public static string RESPONSE_MOUTH_ANIMATION_ALT => GetLocalizedText("RESPONSE_MOUTH_ANIMATION_ALT", "Mouth animation => {0}");
    public static string ERROR_ENABLE_PARAM_MISSING => GetLocalizedText("ERROR_ENABLE_PARAM_MISSING", "ERROR: Missing 'enable' parameter.");
    public static string ERROR_ENABLE_PARAM_INVALID => GetLocalizedText("ERROR_ENABLE_PARAM_INVALID", "ERROR: Invalid 'enable' parameter. Use 'true' or 'false'.");
    public static string RESPONSE_AUTO_PREPARE_SEAMLESS_SET => GetLocalizedText("RESPONSE_AUTO_PREPARE_SEAMLESS_SET", "autoPrepareSeamless set to {0}");
    public static string RESPONSE_AUTO_PREPARE_SEAMLESS_STATUS => GetLocalizedText("RESPONSE_AUTO_PREPARE_SEAMLESS_STATUS", "autoPrepareSeamless: {0}");
    public static string RESPONSE_AUTOBLINK_STATUS => GetLocalizedText("RESPONSE_AUTOBLINK_STATUS", "autoBlink => enabled={0}, freq(ms)={1}");
    public static string RESPONSE_SEAMLESS_BLINK_RESET_CMD => GetLocalizedText("RESPONSE_SEAMLESS_BLINK_RESET_CMD", "Seamless blink reset started. (cmd=reset_blink)");
    public static string RESPONSE_BLINK_RESET_DONE_CMD => GetLocalizedText("RESPONSE_BLINK_RESET_DONE_CMD", "Blink reset done. (cmd=reset_blink)");
    public static string RESPONSE_SEAMLESS_MOUTH_RESET_CMD => GetLocalizedText("RESPONSE_SEAMLESS_MOUTH_RESET_CMD", "[Mouth] seamless reset blendshapes started. (cmd=reset_mouth)");
    public static string RESPONSE_MOUTH_ALL_RESET_CMD => GetLocalizedText("RESPONSE_MOUTH_ALL_RESET_CMD", "[Mouth] all blendshapes reset. (cmd=reset_mouth)");


    public static string LOG_ANIMATION_STOPPED => GetLocalizedText("LOG_ANIMATION_STOPPED", "Animation stopped.");
    public static string ERROR_ANIMATOR_CONTROLLER_NOT_SET => GetLocalizedText("ERROR_ANIMATOR_CONTROLLER_NOT_SET", "Animator or AnimatorController is not set.");
    public static string LOG_VRM_MODEL_LOADED => GetLocalizedText("LOG_VRM_MODEL_LOADED", "VRM model loaded. Applying AnimatorController.");
    public static string LOG_ANIMATIONHANDLER_INITIALIZED => GetLocalizedText("LOG_ANIMATIONHANDLER_INITIALIZED", "AnimationHandler initialization complete.");
    public static string ERROR_CONTROLLER_NOT_SET => GetLocalizedText("ERROR_CONTROLLER_NOT_SET", "AnimatorController is not set in the Inspector.");
    public static string LOG_CONTROLLER_APPLIED => GetLocalizedText("LOG_CONTROLLER_APPLIED", "AnimatorController applied immediately.");
    public static string WARNING_NOT_INITIALIZED => GetLocalizedText("WARNING_NOT_INITIALIZED", "AnimationHandler is not yet initialized.");
    public static string LOG_PLAY_INITIAL_ANIMATION => GetLocalizedText("LOG_PLAY_INITIAL_ANIMATION", "Playing initial animation.");
    public static string ERROR_VRMLOADER_NOT_ATTACHED => GetLocalizedText("ERROR_VRMLOADER_NOT_ATTACHED", "VRMLoader is not attached.");
    public static string WARNING_NOT_INITIALIZED_SKIP => GetLocalizedText("WARNING_NOT_INITIALIZED_SKIP", "AnimationHandler is not yet initialized. Skipping animation.");
    public static string ERROR_ANIMATOR_NOT_SET => GetLocalizedText("ERROR_ANIMATOR_NOT_SET", "Animator is not set.");
    public static string ERROR_INVALID_ANIMATION => GetLocalizedText("ERROR_INVALID_ANIMATION", "Invalid animation: {0}");
    public static string ERROR_INVALID_ANIMATION_ID => GetLocalizedText("ERROR_INVALID_ANIMATION_ID", "Invalid animation ID: {0}");
    public static string ERROR_ANIMATION_NAME_NOT_FOUND => GetLocalizedText("ERROR_ANIMATION_NAME_NOT_FOUND", "Animation name for ID:{0} not found.");
    public static string LOG_SEAMLESS_TRANSITION => GetLocalizedText("LOG_SEAMLESS_TRANSITION", "Seamless transition: Changing to state '{0}' (Layer {1}) in {2} seconds.");
    public static string LOG_FORCE_STATE => GetLocalizedText("LOG_FORCE_STATE", "Forcing state '{0}' (Layer {1}) to maintain state.");
    public static string LOG_ANIMATION_PLAYED => GetLocalizedText("LOG_ANIMATION_PLAYED", "Animation '{0}' played (after Animator configuration).");
    public static string ERROR_VRM_MODEL_NOT_LOADED => GetLocalizedText("ERROR_VRM_MODEL_NOT_LOADED", "VRM model is not loaded.");

    public static string LOG_PREVIOUS_VRMA_DESTROYED => GetLocalizedText("LOG_PREVIOUS_VRMA_DESTROYED", "Previous VRMA instance destroyed.");
    public static string ERROR_VRMA_NOT_FOUND => GetLocalizedText("ERROR_VRMA_NOT_FOUND", ".vrma file not found: {0}");
    public static string ERROR_VRMA_LOAD_FAILED => GetLocalizedText("ERROR_VRMA_LOAD_FAILED", "Failed to load .vrma animation: {0}");
    public static string LOG_VRMA_APPLIED => GetLocalizedText("LOG_VRMA_APPLIED", ".vrma animation '{0}' applied (Loop: {1}).");
    public static string WARNING_VRM_RUNTIME_NOT_FOUND => GetLocalizedText("WARNING_VRM_RUNTIME_NOT_FOUND", "⚠ Vrm10Runtime not found.");
    public static string LOG_ANIMATION_PLAY_STARTED => GetLocalizedText("LOG_ANIMATION_PLAY_STARTED", "Animation playback started!");
    public static string LOG_VRMA_ANIMATION_RESET => GetLocalizedText("LOG_VRMA_ANIMATION_RESET", "VRMA animation reset.");
    public static string LOG_AGIA_RESET => GetLocalizedText("LOG_AGIA_RESET", "AGIA AnimatorController reset and initial animation played.");
    public static string ERROR_VRMA_BINARY_READ_FAILED => GetLocalizedText("ERROR_VRMA_BINARY_READ_FAILED", "Failed to read VRMA file binary data.");
    public static string LOG_VRMA_BINARY_LOAD_SUCCESS => GetLocalizedText("LOG_VRMA_BINARY_LOAD_SUCCESS", "✅ VRMA binary data loaded successfully: {0} bytes");
    public static string ERROR_VRMA_INSTANCE_LOAD_FAILED => GetLocalizedText("ERROR_VRMA_INSTANCE_LOAD_FAILED", "Failed to load VRMA instance.");
    public static string LOG_VRMA_LOADING_START => GetLocalizedText("LOG_VRMA_LOADING_START", "[ImportVrmaAnimation] Starting VRMA load: {0}");
    public static string ERROR_GLTFS_PARSE_FAILED => GetLocalizedText("ERROR_GLTFS_PARSE_FAILED", "[ImportVrmaAnimation] Failed to parse GLTF data.");
    public static string LOG_VRMA_IMPORTER_CREATED => GetLocalizedText("LOG_VRMA_IMPORTER_CREATED", "[ImportVrmaAnimation] VrmAnimationImporter created: {0}");
    public static string ERROR_MATERIAL_NULL => GetLocalizedText("ERROR_MATERIAL_NULL", "[ImportVrmaAnimation] Default material is null in GetDefaultMaterialAsync()!");
    public static string ERROR_SHADER_NULL => GetLocalizedText("ERROR_SHADER_NULL", "[ImportVrmaAnimation] Shader is null in GetDefaultMaterialAsync()!");
    public static string LOG_DEFAULT_MATERIAL => GetLocalizedText("LOG_DEFAULT_MATERIAL", "✅ Default material: {0}, Shader: {1}");
    public static string ERROR_VRMA_INSTANCE_COMPONENT_MISSING => GetLocalizedText("ERROR_VRMA_INSTANCE_COMPONENT_MISSING", "[ImportVrmaAnimation] Vrm10AnimationInstance component missing.");
    public static string LOG_VRMA_INSTANCE_SUCCESS => GetLocalizedText("LOG_VRMA_INSTANCE_SUCCESS", "[ImportVrmaAnimation] Vrm10AnimationInstance acquired successfully: {0}");
    public static string LOG_MATERIAL_LOADED => GetLocalizedText("LOG_MATERIAL_LOADED", "✅ Loaded material: {0}, Shader: {1}");
    public static string ERROR_IMPORT_VRMA_EXCEPTION => GetLocalizedText("ERROR_IMPORT_VRMA_EXCEPTION", "[ImportVrmaAnimation] Exception: {0}\n{1}");
    public static string WARNING_VRMA_INSTANCE_MISSING => GetLocalizedText("WARNING_VRMA_INSTANCE_MISSING", "VRMA instance is missing.");
    public static string LOG_SKINNEDMESH_DISABLED => GetLocalizedText("LOG_SKINNEDMESH_DISABLED", "Disabled SkinnedMeshRenderer: {0}");
    public static string WARNING_ANIMATION_COMPONENT_MISSING => GetLocalizedText("WARNING_ANIMATION_COMPONENT_MISSING", "Animation component is missing. Playback cannot be executed.");
    public static string RESPONSE_ANIMATION_SYSTEM_NOT_INITIALIZED => GetLocalizedText("RESPONSE_ANIMATION_SYSTEM_NOT_INITIALIZED", "Animation system is not initialized.");
    public static string RESPONSE_AGIA_RESET => GetLocalizedText("RESPONSE_AGIA_RESET", "AGIA AnimatorController has been reset and the initial animation has been played.");
    public static string RESPONSE_VRMA_ANIMATION_STARTED => GetLocalizedText("RESPONSE_VRMA_ANIMATION_STARTED", ".vrma animation started: {0} (loop: {1})");
    public static string RESPONSE_ANIMATION_ID_NOT_SPECIFIED => GetLocalizedText("RESPONSE_ANIMATION_ID_NOT_SPECIFIED", "Animation id is not specified.");
    public static string RESPONSE_INVALID_ANIMATION_ID => GetLocalizedText("RESPONSE_INVALID_ANIMATION_ID", "Invalid animation ID: {0}");
    public static string RESPONSE_SEAMLESS_ANIMATION_STARTED => GetLocalizedText("RESPONSE_SEAMLESS_ANIMATION_STARTED", "Animation started with seamless transition.");
    public static string RESPONSE_ANIMATION_STARTED => GetLocalizedText("RESPONSE_ANIMATION_STARTED", "Animation started.");
    public static string RESPONSE_ANIMATION_STOP => GetLocalizedText("RESPONSE_ANIMATION_STOP", "Animation stopped.");

    public static string RESPONSE_SHAPE_PARAM_REQUIRED => GetLocalizedText("RESPONSE_SHAPE_PARAM_REQUIRED", "Shape command requires a shape parameter.");
    public static string ERROR_VRMLOADER_NOT_FOUND => GetLocalizedText("ERROR_VRMLOADER_NOT_FOUND", "VRMLoader not found.");
    public static string ERROR_EXPRESSION_SYSTEM_NOT_AVAILABLE => GetLocalizedText("ERROR_EXPRESSION_SYSTEM_NOT_AVAILABLE", "Expression system is not available.");
    public static string RESPONSE_SEAMLESS_RESET_BLENDSHAPES_STARTED => GetLocalizedText("RESPONSE_SEAMLESS_RESET_BLENDSHAPES_STARTED", "Seamless reset of all blendshapes started.");
    public static string RESPONSE_ALL_BLENDSHAPES_RESET => GetLocalizedText("RESPONSE_ALL_BLENDSHAPES_RESET", "All blendshapes reset successfully.");
    public static string RESPONSE_SEAMLESS_BLENDSHAPE_APPLIED => GetLocalizedText("RESPONSE_SEAMLESS_BLENDSHAPE_APPLIED", "Seamless blendshape '{0}' applied successfully.");
    public static string RESPONSE_BLENDSHAPE_APPLIED => GetLocalizedText("RESPONSE_BLENDSHAPE_APPLIED", "Blendshape '{0}' applied successfully.");
    public static string ERROR_INVALID_BLENDSHAPE_NAME => GetLocalizedText("ERROR_INVALID_BLENDSHAPE_NAME", "Invalid blendshape name: '{0}'.");
    public static string RESPONSE_MOUTH_PARAM_REQUIRED => GetLocalizedText("RESPONSE_MOUTH_PARAM_REQUIRED", "Mouth command requires a word parameter.");
    public static string ERROR_UNSUPPORTED_MOUTH_SHAPE => GetLocalizedText("ERROR_UNSUPPORTED_MOUTH_SHAPE", "Unsupported mouth shape: {0}");
    public static string RESPONSE_SEAMLESS_MOUTH_ANIMATION_STARTED => GetLocalizedText("RESPONSE_SEAMLESS_MOUTH_ANIMATION_STARTED", "Seamless mouth animation for '{0}' started.");
    public static string RESPONSE_MOUTH_ANIMATION_STARTED => GetLocalizedText("RESPONSE_MOUTH_ANIMATION_STARTED", "Mouth animation for '{0}' started.");
    public static string ERROR_INVALID_ANIMATION_COMMAND => GetLocalizedText("ERROR_INVALID_ANIMATION_COMMAND", "Invalid animation command: {0}");
    public static string AUDIOSYNC_ALREADY_ACTIVE => GetLocalizedText("AUDIOSYNC_ALREADY_ACTIVE", "Lip sync is already active. Stopping current session.");
    public static string AUDIOSYNC_INVALID_CHANNEL => GetLocalizedText("AUDIOSYNC_INVALID_CHANNEL", "Invalid channel ID: {0}");
    public static string AUDIOSYNC_MIC_STARTED => GetLocalizedText("AUDIOSYNC_MIC_STARTED", "Microphone lip sync started.");
    public static string AUDIOSYNC_MIC_NOT_FOUND => GetLocalizedText("AUDIOSYNC_MIC_NOT_FOUND", "No microphone devices found.");
    public static string AUDIOSYNC_WASAPI_STARTED => GetLocalizedText("AUDIOSYNC_WASAPI_STARTED", "WASAPI lip sync started.");
    public static string AUDIOSYNC_WASAPI_START_FAILED => GetLocalizedText("AUDIOSYNC_WASAPI_START_FAILED", "Failed to start WASAPI lip sync: {0}");
    public static string AUDIOSYNC_STOPPED => GetLocalizedText("AUDIOSYNC_STOPPED", "Lip sync stopped.");
    public static string AUDIOSYNC_ON_RESPONSE => GetLocalizedText("AUDIOSYNC_ON_RESPONSE", "Lip sync started on channel {0}.");
    public static string AUDIOSYNC_OFF_RESPONSE => GetLocalizedText("AUDIOSYNC_OFF_RESPONSE", "Lip sync turned off.");
    public static string AUDIOSYNC_INVALID_CMD => GetLocalizedText("AUDIOSYNC_INVALID_CMD", "Invalid lip sync command: {0}");
    public static string AUDIOSYNC_RESPONSE_SEND_ERROR => GetLocalizedText("AUDIOSYNC_RESPONSE_SEND_ERROR", "Error sending lip sync response: {0}");
    public static string VRML_RELOADING_IGNORED => GetLocalizedText("VRML_RELOADING_IGNORED", "VRM is currently reloading. New requests are ignored.");
    public static string VRML_EXISTING_MODEL_DESTROYED => GetLocalizedText("VRML_EXISTING_MODEL_DESTROYED", "Destroying existing VRM model.");
    public static string VRML_LOAD_START => GetLocalizedText("VRML_LOAD_START", "Starting VRM load. Path: {0}");
    public static string VRML_LOAD_FAILED => GetLocalizedText("VRML_LOAD_FAILED", "Failed to load VRM.");
    public static string VRML_LOAD_SUCCESS => GetLocalizedText("VRML_LOAD_SUCCESS", "VRM model loaded successfully.");
    public static string VRML_LOAD_EXCEPTION => GetLocalizedText("VRML_LOAD_EXCEPTION", "An exception occurred during VRM load: {0}");
    public static string VRML_UPDATE_WHEN_OFFSCREEN_ENABLED => GetLocalizedText("VRML_UPDATE_WHEN_OFFSCREEN_ENABLED", "Update When Offscreen enabled for: {0}");
    public static string VRML_MAIN_CAMERA_NOT_FOUND => GetLocalizedText("VRML_MAIN_CAMERA_NOT_FOUND", "Main camera not found.");
    public static string VRML_HEAD_BONE_NOT_FOUND => GetLocalizedText("VRML_HEAD_BONE_NOT_FOUND", "Head bone not found.");
    public static string VRML_CAMERA_ADJUSTED => GetLocalizedText("VRML_CAMERA_ADJUSTED", "Camera adjustment complete. Position: {0}, Rotation: {1}");
    public static string ERROR_CONFIG_FILE_NOT_FOUND => GetLocalizedText("ERROR_CONFIG_FILE_NOT_FOUND", "Config file not found: {0}");
    public static string ERROR_CONFIG_LOAD_FAILED => GetLocalizedText("ERROR_CONFIG_LOAD_FAILED", "Failed to load config!");
    public static string ERROR_DIRECTIONAL_LIGHT_NOT_FOUND => GetLocalizedText("ERROR_DIRECTIONAL_LIGHT_NOT_FOUND", "Directional Light not found!");
    public static string LOG_SHADOW_RESOLUTION_SET => GetLocalizedText("LOG_SHADOW_RESOLUTION_SET", "Shadow Resolution set: {0}");
    public static string ERROR_MAIN_CAMERA_NOT_FOUND => GetLocalizedText("ERROR_MAIN_CAMERA_NOT_FOUND", "Main Camera not found!");
    public static string LOG_ANTIALIASING_SET => GetLocalizedText("LOG_ANTIALIASING_SET", "Anti-aliasing set: {0}x");
    public static string WARNING_DIRECTIONAL_LIGHT_CONFIG_NOT_FOUND => GetLocalizedText("WARNING_DIRECTIONAL_LIGHT_CONFIG_NOT_FOUND", "Directional Light or its configuration not found.");
    public static string LOG_DIRECTIONAL_LIGHT_ROTATION_SET => GetLocalizedText("LOG_DIRECTIONAL_LIGHT_ROTATION_SET", "Applied Directional Light rotation: {0}");
    public static string LOG_DIRECTIONAL_LIGHT_RENDERING_SET => GetLocalizedText("LOG_DIRECTIONAL_LIGHT_RENDERING_SET", "Applied Directional Light Rendering settings: RenderingLayerMask={0}, CullingMask={1}");
    public static string ERROR_VRM_MODEL_NULL => GetLocalizedText("ERROR_VRM_MODEL_NULL", "VRM model is null!");
    public static string LOG_SHADOW_SETTINGS_APPLIED => GetLocalizedText("LOG_SHADOW_SETTINGS_APPLIED", "Applied shadow settings: {0}");
    public static string WARNING_MATERIAL_CONFIG_MISSING => GetLocalizedText("WARNING_MATERIAL_CONFIG_MISSING", "Material configuration not found in config.json. Applying default values.");
    public static string LOG_MATERIAL_SHADOW_RECEIVE_SET => GetLocalizedText("LOG_MATERIAL_SHADOW_RECEIVE_SET", "Applied material shadow receive settings: {0}");
    public static string LOG_MATERIAL_SHADOW_GRADE_SET => GetLocalizedText("LOG_MATERIAL_SHADOW_GRADE_SET", "Applied material shadow grade settings: {0}");
    public static string LOG_SHADING_TOONY_SET => GetLocalizedText("LOG_SHADING_TOONY_SET", "Shading Toony set: {0} ({1})");
    public static string LOG_SHADING_SHIFT_SET => GetLocalizedText("LOG_SHADING_SHIFT_SET", "Shading Shift set: {0} ({1})");
    public static string LOG_RIM_COLOR_SET => GetLocalizedText("LOG_RIM_COLOR_SET", "Rim Color set: {0} ({1})");
    public static string LOG_RIM_LIGHTING_MIX_SET => GetLocalizedText("LOG_RIM_LIGHTING_MIX_SET", "Rim Lighting Mix set: {0} ({1})");
    public static string LOG_RIM_FRESNEL_POWER_SET => GetLocalizedText("LOG_RIM_FRESNEL_POWER_SET", "Rim Fresnel Power set: {0} ({1})");
    public static string LOG_RIM_LIFT_SET => GetLocalizedText("LOG_RIM_LIFT_SET", "Rim Lift set: {0} ({1})");
    public static string LOG_OUTLINE_WIDTH_SET => GetLocalizedText("LOG_OUTLINE_WIDTH_SET", "Outline Width set: {0} ({1})");
    public static string LOG_OUTLINE_COLOR_SET => GetLocalizedText("LOG_OUTLINE_COLOR_SET", "Outline Color set: {0} ({1})");
    public static string LOG_OUTLINE_LIGHTING_MIX_SET => GetLocalizedText("LOG_OUTLINE_LIGHTING_MIX_SET", "Outline Lighting Mix set: {0} ({1})");
    public static string WARNING_COLOR_PARSE_FAILED => GetLocalizedText("WARNING_COLOR_PARSE_FAILED", "Failed to parse color: {0}");
    public static string ERROR_DIRECTIONAL_LIGHT_NOT_ASSIGNED => GetLocalizedText("ERROR_DIRECTIONAL_LIGHT_NOT_ASSIGNED", "Directional Light is not assigned. Please set it in the Inspector.");
    public static string ERROR_HEAD_BONE_NOT_FOUND => GetLocalizedText("ERROR_HEAD_BONE_NOT_FOUND", "Head bone not found.");
    public static string LOG_DIRECTIONAL_LIGHT_POSITION_ADJUSTED => GetLocalizedText("LOG_DIRECTIONAL_LIGHT_POSITION_ADJUSTED", "Directional Light position adjusted: {0}, rotation: {1}");
    public static string ERROR_CANVAS_OR_IMAGE_NOT_SET => GetLocalizedText("ERROR_CANVAS_OR_IMAGE_NOT_SET", "Background Canvas or Image is not set. Please configure in the Inspector.");
    public static string LOG_CANVAS_RENDERMODE_SET => GetLocalizedText("LOG_CANVAS_RENDERMODE_SET", "Background Canvas RenderMode set to Screen Space - Camera.");
    public static string ERROR_IMAGE_FILE_NOT_FOUND => GetLocalizedText("ERROR_IMAGE_FILE_NOT_FOUND", "Image file not found: {0}. Background switching and initialization aborted.");
    public static string ERROR_IMAGE_LOAD_FAILED => GetLocalizedText("ERROR_IMAGE_LOAD_FAILED", "Failed to load image: {0}. Background switching and initialization aborted.");
    public static string WARNING_IMAGE_OR_SPRITE_NOT_SET => GetLocalizedText("WARNING_IMAGE_OR_SPRITE_NOT_SET", "Background Image or Sprite not set. Skipping size adjustment.");
    public static string ERROR_RECTTRANSFORM_NOT_FOUND => GetLocalizedText("ERROR_RECTTRANSFORM_NOT_FOUND", "RectTransform not found.");
    public static string WARNING_BACKGROUND_IMAGE_NOT_SET => GetLocalizedText("WARNING_BACKGROUND_IMAGE_NOT_SET", "Background image not set. Skipping Canvas position adjustment.");
    public static string LOG_CANVAS_POSITION_ADJUSTED => GetLocalizedText("LOG_CANVAS_POSITION_ADJUSTED", "Background Canvas position adjusted: {0}");
    public static string LOG_VRM_MODEL_LOADED_BACKGROUND => GetLocalizedText("LOG_VRM_MODEL_LOADED_BACKGROUND", "VRM model loaded.");
    public static string LOG_BACKGROUND_IMAGE_VALID => GetLocalizedText("LOG_BACKGROUND_IMAGE_VALID", "Background image is valid, adjusting Canvas position.");
    public static string RESPONSE_FILE_PARAM_MISSING => GetLocalizedText("RESPONSE_FILE_PARAM_MISSING", "The 'file' parameter is missing.");
    public static string ERROR_IMAGE_LOAD_FAILURE => GetLocalizedText("ERROR_IMAGE_LOAD_FAILURE", "Image file not found or failed to load: {0}");
    public static string RESPONSE_BACKGROUND_IMAGE_CHANGED => GetLocalizedText("RESPONSE_BACKGROUND_IMAGE_CHANGED", "Background image switched to {0}.");
    public static string ERROR_INVALID_BACKGROUND_CMD => GetLocalizedText("ERROR_INVALID_BACKGROUND_CMD", "Invalid background command: {0}");
    public static string ERROR_RESPONSE_SEND => GetLocalizedText("ERROR_RESPONSE_SEND", "[HttpCommandHandlerBase] Response sending error: {0}");
    public static string RESPONSE_LIPSYNC_ON => GetLocalizedText("RESPONSE_LIPSYNC_ON", "LipSync started on channel {0}!");
    public static string RESPONSE_LIPSYNC_OFF => GetLocalizedText("RESPONSE_LIPSYNC_OFF", "LipSync stopped!");
    public static string ERROR_INVALID_LIPSYNC_CMD => GetLocalizedText("ERROR_INVALID_LIPSYNC_CMD", "Invalid lip sync command: {0}");
    public static string RESPONSE_SERVER_TERMINATE => GetLocalizedText("RESPONSE_SERVER_TERMINATE", "Server is shutting down.");
    public static string ERROR_INVALID_SERVER_CMD => GetLocalizedText("ERROR_INVALID_SERVER_CMD", "Invalid server command: {0}");
    public static string LOG_SERVER_SHUTDOWN_INITIATED => GetLocalizedText("LOG_SERVER_SHUTDOWN_INITIATED", "[ServerCommandHandler] Server shutdown initiated.");
    public static string ERROR_VRM_FILE_NOT_FOUND => GetLocalizedText("ERROR_VRM_FILE_NOT_FOUND", "VRM file not found: {0}");
    public static string RESPONSE_VRM_LOADED => GetLocalizedText("RESPONSE_VRM_LOADED", "VRM loaded: {0}");
    public static string LOG_VRM_LOAD_REQUEST => GetLocalizedText("LOG_VRM_LOAD_REQUEST", "[VrmCommandHandler] New VRM load request. File path: {0}");
    public static string ERROR_INVALID_VRM_CMD => GetLocalizedText("ERROR_INVALID_VRM_CMD", "Invalid vrm cmd: {0}");
    public static string ERROR_UNKNOWN_COMMAND => GetLocalizedText("ERROR_UNKNOWN_COMMAND", "Unknown command received. Please check your request.");
    public static string ERROR_VRM_NOT_LOADED => GetLocalizedText("ERROR_VRM_NOT_LOADED", "VRM model is not loaded.");
    public static string ERROR_PARAM_XYZ_MISSING => GetLocalizedText("ERROR_PARAM_XYZ_MISSING", "Missing xyz parameter.");
    public static string ERROR_PARAM_XYZ_INVALID_FORMAT => GetLocalizedText("ERROR_PARAM_XYZ_INVALID_FORMAT", "Invalid format for xyz parameter.");
    public static string RESPONSE_CURRENT_POSITION => GetLocalizedText("RESPONSE_CURRENT_POSITION", "Current position: {0}");
    public static string RESPONSE_CURRENT_ROTATION => GetLocalizedText("RESPONSE_CURRENT_ROTATION", "Current rotation: {0}");
    public static string RESPONSE_ROTATION_SET => GetLocalizedText("RESPONSE_ROTATION_SET", "Rotation set to {0}.");
    public static string ERROR_DEPRECATED_COMMAND => GetLocalizedText("ERROR_DEPRECATED_COMMAND", "The command '{0}' is deprecated and no longer supported.");
    public static string INFO_ROTATION_LIMITED_BY_LICENSE = "X and Z axis rotation were limited to ±20 degrees due to license restrictions (sexual expression not allowed).";




    private static string GetLocalizedText(string key, string defaultText) {
        if (localizedText.ContainsKey(key)) {
            return localizedText[key];
        }
        else {
            Debug.LogWarning($"Localization key '{key}' not found. Using default text.");
            return defaultText;
        }
    }
}

[System.Serializable]
public class LocalizationData {
    public ResourceStrings resourceStrings;
}

[System.Serializable]
public class ResourceStrings {
    public string default_lang;
    public Dictionary<string, Dictionary<string, string>> resourceStrings;
}
