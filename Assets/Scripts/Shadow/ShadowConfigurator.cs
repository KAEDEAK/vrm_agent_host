using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

#region Config Classes

[System.Serializable]
public class ShadowConfig
{
    public float shadowStrength = 0.8f;
    public float shadowBias = 0.02f;
    public float shadowNormalBias = 0.3f;
    public string shadowResolution = "High"; // "Low", "Medium", "High", "VeryHigh"
}

[System.Serializable]
public class CameraConfig
{
    public int antiAliasing = 4;  // 0 = Off, 2x, 4x, 8x
}

[System.Serializable]
public class MaterialConfig
{
    public float shadingToony = 1.0f;
    public float shadingShift = 0.5f;
}

[System.Serializable]
public class RimConfig
{
    public string color = "#48203C";
    public float lightingMix = 1.0f;
    public float fresnelPower = 45;
    public float lift = 0.1f;
}

[System.Serializable]
public class OutlineConfig
{
    public float width = 0.05f;
    public string color = "#919191";
    public float lightingMix = 1.0f;
}

[System.Serializable]
public class DirectionalLightConfig
{
    public Vector3 rotation = new Vector3(30, 45, 0);
}

[System.Serializable]
public class DirectionalLightRenderingConfig
{
    public bool useDefault = true;
    public int cullingMask = -1;
}

[System.Serializable]
public class ConfigData
{
    public ShadowConfig shadows = new ShadowConfig();
    public CameraConfig camera = new CameraConfig();
    public MaterialConfig materials = new MaterialConfig();
    public RimConfig rim = new RimConfig();
    public OutlineConfig outline = new OutlineConfig();
    public DirectionalLightConfig directionalLightConfig = new DirectionalLightConfig();
    public DirectionalLightRenderingConfig directionalLightRendering = new DirectionalLightRenderingConfig();
}

#endregion

public class ShadowConfigurator : MonoBehaviour
{
    public Light directionalLight;
    public Camera mainCamera;

    private string configPath;
    private VRMLoader vrmLoader;
    private ConfigData loadedConfig;

    private void Start()
    {
        configPath = UserPaths.ConfigPath;
        loadedConfig = LoadConfigSettings();

        vrmLoader = GetComponent<VRMLoader>();
        if (vrmLoader != null)
        {
            vrmLoader.OnVRMLoadComplete += OnVrmModelLoaded;
        }
        else
        {
            Debug.LogError(i18nMsg.ERROR_VRMLOADER_NOT_ATTACHED);
        }
    }

    private void OnDestroy()
    {
        if (vrmLoader != null)
        {
            vrmLoader.OnVRMLoadComplete -= OnVrmModelLoaded;
        }
    }

    private void OnVrmModelLoaded(GameObject vrmModel)
    {
        ConfigureShadowSettings(vrmModel, loadedConfig);
    }
    private ConfigData LoadConfigSettings()
    {
        // 1) とりあえず基本のデフォルトConfigを用意
        ConfigData defaultConfig = new ConfigData(); // ← ここで shadows など既定値がセットされる

        // 2) ファイルが無ければデフォルトを返す
        if (!File.Exists(configPath))
        {
            Debug.LogWarning($"[ShadowConfigurator] config.json が見つからないのでデフォルト設定を使うっす！ path={configPath}");
            return defaultConfig;
        }

        // 3) ファイルがあるので読み込みトライ
        string jsonText = File.ReadAllText(configPath);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            Debug.LogWarning($"[ShadowConfigurator] config.json が空っぽだったのでデフォルト設定を使うっす！ path={configPath}");
            return defaultConfig;
        }

        // 4) パース
        ConfigData loaded = JsonUtility.FromJson<ConfigData>(jsonText);
        if (loaded == null)
        {
            Debug.LogWarning($"[ShadowConfigurator] config.json の読み込みに失敗したからデフォルト設定を使うっす！ path={configPath}");
            return defaultConfig;
        }

        // 5) 正常ロードできたならそれを返す
        return loaded;
    }

    private void ConfigureShadowSettings(GameObject vrmModel, ConfigData config)
    {
        if (vrmModel == null)
        {
            Debug.LogError(i18nMsg.ERROR_VRM_MODEL_NULL);
            return;
        }

        // まずシャドウ系・カメラ系など設定を適用
        if (config.shadows != null)
        {
            ApplyDirectionalLightSettings(config.shadows);
        }
        else
        {
            // もし null だったらデフォルト ShadowConfig を再適用
            Debug.LogWarning("[ShadowConfigurator] config.shadows が null なのでデフォルトShadowConfigを使うっす！");
            ApplyDirectionalLightSettings(new ShadowConfig());
        }

        if (config.camera != null)
        {
            ApplyCameraSettings(config.camera);
        }
        else
        {
            Debug.LogWarning("[ShadowConfigurator] config.camera が null なのでデフォルトCameraConfigを使うっす！");
            ApplyCameraSettings(new CameraConfig());
        }

        // モデルの各レンダラーに影設定
        Renderer[] renderers = vrmModel.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            r.receiveShadows = true;
            r.shadowCastingMode = ShadowCastingMode.On;
            Debug.Log(string.Format(i18nMsg.LOG_SHADOW_SETTINGS_APPLIED, r.name));
        }

        // Material 設定の適用
        if (config.materials != null)
        {
            ApplyMaterialShadowSettingsFromConfig(vrmModel, config.materials);
        }
        else
        {
            Debug.LogWarning("[ShadowConfigurator] config.materials が null なのでデフォルトMaterialConfigを使うっす！");
            ApplyMaterialShadowSettingsFromConfig(vrmModel, new MaterialConfig());
        }

        // Rim と Outline 設定の適用
        if (config.rim != null)
        {
            ApplyRimSettings(vrmModel, config.rim);
        }
        if (config.outline != null)
        {
            ApplyOutlineSettings(vrmModel, config.outline);
        }

        // Directional Light の回転＆レンダリング設定
        if (config.directionalLightConfig != null && config.directionalLightRendering != null)
        {
            ApplyDirectionalLightFullConfig(config.directionalLightConfig, config.directionalLightRendering);
        }
    }
    private void ApplyDirectionalLightSettings(ShadowConfig shadowConfig)
    {
        if (directionalLight == null)
        {
            Debug.LogError(i18nMsg.ERROR_DIRECTIONAL_LIGHT_NOT_FOUND);
            return;
        }

        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 0.75f;
        directionalLight.color = Color.white;
        directionalLight.shadowStrength = shadowConfig.shadowStrength;
        directionalLight.shadowBias = shadowConfig.shadowBias;
        directionalLight.shadowNormalBias = shadowConfig.shadowNormalBias;
        directionalLight.cullingMask = ~0;

        if (!string.IsNullOrEmpty(shadowConfig.shadowResolution))
        {
            QualitySettings.shadowResolution = (ShadowResolution)System.Enum.Parse(typeof(ShadowResolution), shadowConfig.shadowResolution);
            Debug.Log(string.Format(i18nMsg.LOG_SHADOW_RESOLUTION_SET, QualitySettings.shadowResolution));
        }
    }
    
    private void ApplyCameraSettings(CameraConfig camConfig)
    {
        if (mainCamera == null)
        {
            Debug.LogError(i18nMsg.ERROR_MAIN_CAMERA_NOT_FOUND);
            return;
        }
        mainCamera.allowMSAA = camConfig.antiAliasing > 0;
        QualitySettings.antiAliasing = camConfig.antiAliasing;
        Debug.Log(string.Format(i18nMsg.LOG_ANTIALIASING_SET, QualitySettings.antiAliasing));
    }

    // Directional Light の回転と Rendering 設定を統合して適用する
    private void ApplyDirectionalLightFullConfig(DirectionalLightConfig lightConfig, DirectionalLightRenderingConfig renderConfig)
    {
        if (directionalLight == null || lightConfig == null || renderConfig == null)
        {
            Debug.LogWarning(i18nMsg.WARNING_DIRECTIONAL_LIGHT_CONFIG_NOT_FOUND);
            return;
        }
        // 回転設定
        directionalLight.transform.rotation = Quaternion.Euler(lightConfig.rotation);
        Debug.Log(string.Format(i18nMsg.LOG_DIRECTIONAL_LIGHT_ROTATION_SET, lightConfig.rotation));
        
        // Rendering 設定
        directionalLight.renderingLayerMask = renderConfig.useDefault ? 1 : 0;
        directionalLight.cullingMask = renderConfig.cullingMask;
        Debug.Log(string.Format(i18nMsg.LOG_DIRECTIONAL_LIGHT_RENDERING_SET, (renderConfig.useDefault ? 1 : 0), renderConfig.cullingMask));
    }

    private void ApplyMaterialShadowSettingsFromConfig(GameObject vrmModel, MaterialConfig materialConfig)
    {
        Renderer[] renderers = vrmModel.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                if (material.shader.name.Contains("VRM/MToon"))
                {
                    if (material.HasProperty("_ReceiveShadowRate"))
                    {
                        material.SetFloat("_ReceiveShadowRate", 1.0f);
                        Debug.Log(string.Format(i18nMsg.LOG_MATERIAL_SHADOW_RECEIVE_SET, material.name));
                    }
                    if (material.HasProperty("_ShadingGradeRate"))
                    {
                        material.SetFloat("_ShadingGradeRate", -0.5f);
                        Debug.Log(string.Format(i18nMsg.LOG_MATERIAL_SHADOW_GRADE_SET, material.name));
                    }
                    if (material.HasProperty("_ShadingToony"))
                    {
                        material.SetFloat("_ShadingToony", materialConfig.shadingToony);
                        Debug.Log(string.Format(i18nMsg.LOG_SHADING_TOONY_SET, materialConfig.shadingToony, material.name));
                    }
                    if (material.HasProperty("_ShadingShift"))
                    {
                        material.SetFloat("_ShadingShift", materialConfig.shadingShift);
                        Debug.Log(string.Format(i18nMsg.LOG_SHADING_SHIFT_SET, materialConfig.shadingShift, material.name));
                    }
                }
            }
        }
    }

    private void ApplyRimSettings(GameObject vrmModel, RimConfig rimConfig)
    {
        Renderer[] renderers = vrmModel.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                if (material.shader.name.Contains("VRM/MToon"))
                {
                    if (material.HasProperty("_RimColor"))
                    {
                        material.SetColor("_RimColor", ParseColor(rimConfig.color));
                        Debug.Log(string.Format(i18nMsg.LOG_RIM_COLOR_SET, rimConfig.color, material.name));
                    }
                    if (material.HasProperty("_RimLightingMix"))
                    {
                        material.SetFloat("_RimLightingMix", rimConfig.lightingMix);
                        Debug.Log(string.Format(i18nMsg.LOG_RIM_LIGHTING_MIX_SET, rimConfig.lightingMix, material.name));
                    }
                    if (material.HasProperty("_RimFresnelPower"))
                    {
                        material.SetFloat("_RimFresnelPower", rimConfig.fresnelPower);
                        Debug.Log(string.Format(i18nMsg.LOG_RIM_FRESNEL_POWER_SET, rimConfig.fresnelPower, material.name));
                    }
                    if (material.HasProperty("_RimLift"))
                    {
                        material.SetFloat("_RimLift", rimConfig.lift);
                        Debug.Log(string.Format(i18nMsg.LOG_RIM_LIFT_SET, rimConfig.lift, material.name));
                    }
                }
            }
        }
    }

    private void ApplyOutlineSettings(GameObject vrmModel, OutlineConfig outlineConfig)
    {
        Renderer[] renderers = vrmModel.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                if (material.shader.name.Contains("VRM/MToon"))
                {
                    if (material.HasProperty("_OutlineWidth"))
                    {
                        material.SetFloat("_OutlineWidth", outlineConfig.width);
                        Debug.Log(string.Format(i18nMsg.LOG_OUTLINE_WIDTH_SET, outlineConfig.width, material.name));
                    }
                    if (material.HasProperty("_OutlineColor"))
                    {
                        material.SetColor("_OutlineColor", ParseColor(outlineConfig.color));
                        Debug.Log(string.Format(i18nMsg.LOG_OUTLINE_COLOR_SET, outlineConfig.color, material.name));
                    }
                    if (material.HasProperty("_OutlineLightingMix"))
                    {
                        material.SetFloat("_OutlineLightingMix", outlineConfig.lightingMix);
                        Debug.Log(string.Format(i18nMsg.LOG_OUTLINE_LIGHTING_MIX_SET, outlineConfig.lightingMix, material.name));
                    }
                }
            }
        }
    }

    // ヘルパー：16進数カラー文字列を Color に変換
    private Color ParseColor(string hex)
    {
        Color color;
        if (ColorUtility.TryParseHtmlString(hex, out color))
            return color;
        Debug.LogWarning(string.Format(i18nMsg.WARNING_COLOR_PARSE_FAILED, hex));
        return Color.white;
    }

    private void AdjustLightPosition(GameObject vrmModel)
    {
        if (directionalLight == null)
        {
            Debug.LogError(i18nMsg.ERROR_DIRECTIONAL_LIGHT_NOT_ASSIGNED);
            return;
        }
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError(i18nMsg.ERROR_MAIN_CAMERA_NOT_FOUND);
            return;
        }
        Transform headBone = GetHeadBone(vrmModel);
        if (headBone == null)
        {
            Debug.LogError(i18nMsg.ERROR_HEAD_BONE_NOT_FOUND);
            return;
        }
        Vector3 facePosition = headBone.position;
        Vector3 modelForward = GetModelForward(vrmModel);
        Vector3 lightPosition = facePosition + modelForward * 2.0f + Vector3.up * 4.0f;
        directionalLight.transform.position = lightPosition;
        directionalLight.transform.LookAt(facePosition);
        Debug.Log(string.Format(i18nMsg.LOG_DIRECTIONAL_LIGHT_POSITION_ADJUSTED, directionalLight.transform.position, directionalLight.transform.rotation.eulerAngles));
    }

    private Transform GetHeadBone(GameObject model)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
                return headBone;
        }
        return FindNodeByName(model.transform, new string[] { "Head", "J_Bip_C_Head" });
    }

    private Vector3 GetModelForward(GameObject model)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
                return hips.forward;
        }
        Transform hipsNode = FindNodeByName(model.transform, new string[] { "Hips", "Root", "J_Bip_C_Hips" });
        return hipsNode != null ? hipsNode.forward : Vector3.forward;
    }

    private Transform FindNodeByName(Transform parent, string[] nodeNames)
    {
        foreach (Transform child in parent)
        {
            foreach (string nodeName in nodeNames)
            {
                if (child.name.Contains(nodeName))
                    return child;
            }
            Transform result = FindNodeByName(child, nodeNames);
            if (result != null)
                return result;
        }
        return null;
    }
}
