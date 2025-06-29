using UnityEngine;
using System;
using System.IO;
using System.Threading.Tasks;
using UniGLTF;
using UniVRM10;  // 使用中の UniVRM10 のバージョンに合わせる
using VRM;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif
using System.Collections;
using System.Collections.Generic;

public class AnimationHandler : MonoBehaviour {
    [Header("Animator Controller")]
    public RuntimeAnimatorController externalController; // Inspector で設定可能

    private Animator animator;
    private GameObject vrmModel;
    private string currentState = "";
    private bool isInitialized = false; // 初期化完了フラグ

    public bool IsInitialized { get { return isInitialized; } }
    private List<string> animationStates = new List<string>();
    private int lastAnimationID = -1;
    private string lastAnimationCategory = "";
    public int LastAnimationID { get { return lastAnimationID; } }
    public string LastAnimationCategory { get { return lastAnimationCategory; } }

    public delegate void AnimationReadyHandler();
    public event AnimationReadyHandler onAnimationReady;

    // VRMA再生用に VRMLoader への参照を保持
    private VRMLoader vrmLoader;

    // 現在再生中の VRMA インスタンスを保持（これを破棄する）
    private Vrm10AnimationInstance currentVrmaInstance = null;

    public void StopAnimation() {
        if (animator != null) {
            animator.speed = 0f;
            Debug.Log(i18nMsg.LOG_ANIMATION_STOPPED);
        }
    }
    public void ResumeAnimation() {
        if (animator != null && animator.speed == 0f) {
            animator.speed = 1.0f;
            Debug.Log(i18nMsg.RESPONSE_ANIMATION_STARTED);
        }
    }

    private void Update() {
        if (animator == null) return;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        // 必要ならログ出力
        // Debug.Log($"Current State: {currentState}, Normalized Time: {stateInfo.normalizedTime}");
    }

    private IEnumerator ApplyInitialAnimation() {
        yield return null; // 1フレーム待機
        PlayAnimationByName("Idle_generic_01");
    }

    private void ExtractAnimationStates() {
        animationStates.Clear();
        if (animator == null || animator.runtimeAnimatorController == null) {
            Debug.LogError(i18nMsg.ERROR_ANIMATOR_CONTROLLER_NOT_SET);
            return;
        }
#if UNITY_EDITOR
        var controller = externalController as AnimatorController;
        if (controller != null) {
            foreach (var layer in controller.layers) {
                foreach (var state in layer.stateMachine.states) {
                    animationStates.Add(state.state.name);
                }
            }
        }
#else
        foreach (AnimationClip clip in externalController.animationClips)
        {
            animationStates.Add(clip.name);
        }
#endif
    }

    private IEnumerator ApplyInitialAnimationWithDelay() {
        yield return new WaitForSeconds(1.0f);
        yield return null;
        PlayAnimationByName("Idle_generic_01");
        isInitialized = true;
    }

    private int GetAnimationID(string category, string animationName) {
        var config = ServerConfig.Instance;
        return config.GetAnimationId(category, animationName);
    }

    private void OnModelLoaded(GameObject loadedModel) {
        Debug.Log(i18nMsg.LOG_VRM_MODEL_LOADED);
        vrmModel = loadedModel;
        vrmModel.transform.localPosition = Vector3.zero;
        vrmModel.SetActive(true);
        InitializeAnimator();
        ExtractAnimationStates();
        isInitialized = true;
        Debug.Log(i18nMsg.LOG_ANIMATIONHANDLER_INITIALIZED);
        onAnimationReady?.Invoke();
    }

    private void InitializeAnimator() {
        animator = vrmModel.GetComponent<Animator>() ?? vrmModel.AddComponent<Animator>();
        if (externalController == null) {
            Debug.LogError(i18nMsg.ERROR_CONTROLLER_NOT_SET);
            return;
        }
        animator.runtimeAnimatorController = externalController;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.speed = 1.0f;
        animator.enabled = true;
        Debug.Log(i18nMsg.LOG_CONTROLLER_APPLIED);
    }

    private void OnAnimationReady() {
        if (!isInitialized) {
            Debug.LogWarning(i18nMsg.WARNING_NOT_INITIALIZED);
            return;
        }
        Debug.Log(i18nMsg.LOG_PLAY_INITIAL_ANIMATION);
        PlayAnimationByName("Idle_generic_01");
    }

    private void Start() {
        vrmLoader = GetComponent<VRMLoader>();
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete += OnModelLoaded;
        }
        else {
            Debug.LogError(i18nMsg.ERROR_VRMLOADER_NOT_ATTACHED);
        }
        onAnimationReady += OnAnimationReady;
    }

    private void OnDestroy() {
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnModelLoaded;
        }
        onAnimationReady -= OnAnimationReady;
    }

    // 旧来のメソッド。HTTP 経由での再生は、PlayAnimationByID / PlayAnimationByState を使用する前提。
    public void PlayAnimationByName(string stateName) {
        if (!isInitialized) {
            Debug.LogWarning(i18nMsg.WARNING_NOT_INITIALIZED_SKIP);
            return;
        }
        if (animator == null) {
            Debug.LogError(i18nMsg.ERROR_ANIMATOR_NOT_SET);
            return;
        }
        string category = "Idle";
        if (stateName.StartsWith("Other_"))
            category = "Other";
        else if (stateName.StartsWith("Layer_"))
            category = "Layer";
        string animationKey = stateName.Replace(category + "_", "").ToLower();
        animationKey = animationKey.Split('_')[0];
        int animationID = GetAnimationID(category, animationKey);
        if (animationID == -1) {
            Debug.LogError(string.Format(i18nMsg.ERROR_INVALID_ANIMATION, stateName));
            return;
        }
        PlayAnimationByID(animationID, category);
    }

    private IEnumerator ForceStateAfterTransition(string stateKey, int layer, float delay) {
        yield return new WaitForSeconds(delay);
        animator.Play(stateKey, layer, 0f);
        currentState = stateKey;
        Debug.Log(string.Format(i18nMsg.LOG_FORCE_STATE, stateKey, layer));
    }

    // 従来の IntBased 再生メソッド。HTTP 経由からはこちらが Idle, Other 用に呼ばれる。
    public void PlayAnimationByID(int animationID, string category, bool seamless = false) {
        if (!isInitialized) {
            Debug.LogWarning(i18nMsg.WARNING_NOT_INITIALIZED_SKIP);
            return;
        }
        if (animator == null) {
            Debug.LogError(i18nMsg.ERROR_ANIMATOR_NOT_SET);
            return;
        }
        if (animationID == -1) {
            Debug.LogError(string.Format(i18nMsg.ERROR_INVALID_ANIMATION_ID, animationID));
            return;
        }
        MainThreadInvoker.Invoke(() => {
            if (seamless) {
                var config = ServerConfig.Instance;
                string animKey = config.GetAnimationName(category, animationID);
                if (string.IsNullOrEmpty(animKey)) {
                    Debug.LogError(string.Format(i18nMsg.ERROR_ANIMATION_NAME_NOT_FOUND, animationID));
                    return;
                }
                string stateKey = category + "_" + (animKey.Contains("_") ? animKey : animKey + "_01");
                int layer = (category == "Layer") ? 1 : 0;
                float transitionDuration = 0.3f;
                if (category == "Idle") {
                    animator.SetInteger("animBaseInt", animationID);
                }
                else if (category == "Other") {
                    animator.SetInteger("animOtherInt", animationID);
                }
                animator.CrossFadeInFixedTime(stateKey, transitionDuration, layer, 0f);
                currentState = stateKey;
                lastAnimationID = animationID;
                lastAnimationCategory = category;
                Debug.Log(string.Format(i18nMsg.LOG_SEAMLESS_TRANSITION, stateKey, layer, transitionDuration));
                StartCoroutine(ForceStateAfterTransition(stateKey, layer, transitionDuration));
            }
            else {
                if (category == "Idle" || category == "Other") {
                    animator.SetInteger("animBaseInt", category == "Idle" ? animationID : 0);
                    animator.SetInteger("animOtherInt", category == "Other" ? animationID : 0);
                }
                else if (category == "Layer") {
                    Debug.Log(string.Format(i18nMsg.LOG_SEAMLESS_TRANSITION, $"Layer_{animationID}", 1, 0.0f));
                    animator.Play($"Layer_{animationID}", 1, 0.0f);
                }
                lastAnimationID = animationID;
                lastAnimationCategory = category;
                string stateName = (category == "Layer") ? $"Layer_{animationID}" : $"ID_{animationID}";
                StartCoroutine(WaitForParameterUpdateAndPlay(stateName));
            }
        });
    }

    private IEnumerator WaitForParameterUpdateAndPlay(string stateName) {
        yield return null;
        animator.Play(stateName);
        currentState = stateName;
        Debug.Log(string.Format(i18nMsg.LOG_ANIMATION_PLAYED, stateName));
    }

    public string GetAnimationStatusJson() {
        var statusInfo = new Dictionary<string, object>();
        statusInfo["currentAnimation"] = currentState;
        statusInfo["availableStates"] = animationStates;
        statusInfo["isInitialized"] = isInitialized;
        return SimpleJsonBuilder.Serialize(statusInfo);
    }

    [Serializable]
    public class AnimationStatusInfo {
        public string currentAnimation;
        public List<string> availableStates;
    }

    // =========================
    // 以下、VRMA再生用の実装
    // =========================

    public async void PlayVrmaAnimation(string file, bool loop, string nextState = "Idle_generic_01", bool seamless = false) {
        if (vrmModel == null) {
            Debug.LogError(i18nMsg.ERROR_VRM_MODEL_NOT_LOADED);
            return;
        }

        if (currentVrmaInstance != null) {
            Destroy(currentVrmaInstance.gameObject);
            currentVrmaInstance = null;
            Debug.Log(i18nMsg.LOG_PREVIOUS_VRMA_DESTROYED);
        }

        string projectRootPath = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRootPath, UserPaths.VRMA_FOLDER, file);
        fullPath = Path.GetFullPath(fullPath);

        if (!File.Exists(fullPath)) {
            Debug.LogError(string.Format(i18nMsg.ERROR_VRMA_NOT_FOUND, fullPath));
            return;
        }

        Vrm10AnimationInstance vrmaAnimation = await ImportVrmaAnimation(fullPath, loop);
        if (vrmaAnimation == null) {
            Debug.LogError(string.Format(i18nMsg.ERROR_VRMA_LOAD_FAILED, file));
            return;
        }

        currentVrmaInstance = vrmaAnimation;
        HideBoxManFromAnimationInstance(vrmaAnimation);

        var runtime = vrmLoader.VrmInstance.Runtime;
        if (runtime != null) {
            runtime.VrmAnimation = vrmaAnimation;
            Debug.Log(string.Format(i18nMsg.LOG_VRMA_APPLIED, file, loop));
        }
        else {
            Debug.LogWarning(i18nMsg.WARNING_VRM_RUNTIME_NOT_FOUND);
        }

        Animation anim = vrmaAnimation.GetComponent<Animation>();
        if (anim != null) {
            anim.clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            anim.Play();
            Debug.Log(i18nMsg.LOG_ANIMATION_PLAY_STARTED);

            if (!loop) {
                StartCoroutine(WaitForVrmaToFinish(vrmaAnimation, nextState));
            }
        }
        else {
            Debug.LogWarning(i18nMsg.WARNING_ANIMATION_COMPONENT_MISSING);
        }
    }

    private IEnumerator WaitForVrmaToFinish(Vrm10AnimationInstance vrmaInstance, string nextState = "") {
        if (vrmaInstance == null) {
            yield break;
        }

        Animation anim = vrmaInstance.GetComponent<Animation>();
        if (anim == null || anim.clip == null) {
            Debug.LogWarning("⚠ VRMA animation component is missing.");
            yield break;
        }

        yield return new WaitForSeconds(anim.clip.length);

        Debug.Log($"🎬 VRMA animation finished. Preparing to switch to {nextState}");

        if (vrmLoader.VrmInstance != null && vrmLoader.VrmInstance.Runtime != null) {
            vrmLoader.VrmInstance.Runtime.VrmAnimation = null;
        }

        yield return null;

        if (animator != null) {
            Dictionary<Transform, Quaternion> savedSpring = null;
            if (vrmLoader != null && vrmLoader.VrmInstance != null)
            {
                savedSpring = CaptureSpringBoneRotations(vrmLoader.VrmInstance.gameObject);
            }

            animator.Rebind();
            animator.Update(0);
            animator.runtimeAnimatorController = externalController;

            if (savedSpring != null)
            {
                StartCoroutine(RestoreSpringBoneRotationsNextFrame(savedSpring));
            }

            if (string.IsNullOrEmpty(nextState)) {
                Debug.Log("🔄 No next state provided. Returning to default animation: Idle_generic_01");
                PlayAnimationByName("Idle_generic_01");
                yield break;
            }

            Debug.Log($"✅ Switching to animation via PlayAnimationByName: {nextState}");
            PlayAnimationByName(nextState);
        }
        else {
            Debug.LogError("❌ ERROR: Animator is null, cannot switch animation.");
        }
    }

    public void ResetAGIAAnimation() {
        if (animator == null) {
            Debug.LogError(i18nMsg.ERROR_ANIMATOR_NOT_SET);
            return;
        }

        if (vrmLoader.VrmInstance != null && vrmLoader.VrmInstance.Runtime != null) {
            vrmLoader.VrmInstance.Runtime.VrmAnimation = null;
            Debug.Log(i18nMsg.LOG_VRMA_ANIMATION_RESET);
        }

        animator.runtimeAnimatorController = externalController;
        animator.Rebind();
        animator.Update(0);
        PlayAnimationByName("Idle_generic_01");
        Debug.Log(i18nMsg.LOG_AGIA_RESET);
    }

    // ===============================
    // 新規追加：PlayBased 再生用メソッド
    // ===============================
    public void PlayAnimationByState(string stateName, int layer, float normalizedTime) {
        if (animator == null) {
            Debug.LogError("PlayAnimationByState: Animator is not set.");
            return;
        }
        animator.Play(stateName, layer, normalizedTime);
        Debug.Log(string.Format(i18nMsg.LOG_ANIMATION_PLAYED, stateName));
    }

    /// <summary>
    /// 指定した .vrma ファイルを読み込み、Vrm10AnimationInstance を返す非同期メソッド
    /// </summary>
    private async Task<Vrm10AnimationInstance> _ImportVrmaAnimation(string fullPath, bool loop) {
        try {
            byte[] vrmaBytes = await File.ReadAllBytesAsync(fullPath);
            if (vrmaBytes == null || vrmaBytes.Length == 0) {
                Debug.LogError(i18nMsg.ERROR_VRMA_BINARY_READ_FAILED);
                return null;
            }
            Debug.Log(string.Format(i18nMsg.LOG_VRMA_BINARY_LOAD_SUCCESS, vrmaBytes.Length));
            GltfData data = new GlbBinaryParser(vrmaBytes, fullPath).Parse();

            VrmAnimationImporter importer = new VrmAnimationImporter(data);
            var instance = await importer.LoadAsync(new ImmediateCaller());
            if (instance == null) {
                Debug.LogError(i18nMsg.ERROR_VRMA_INSTANCE_LOAD_FAILED);
                return null;
            }
            Vrm10AnimationInstance vrmaInstance = instance.GetComponent<Vrm10AnimationInstance>();
            return vrmaInstance;
        }
        catch (Exception ex) {
            Debug.LogError(string.Format(i18nMsg.ERROR_IMPORT_VRMA_EXCEPTION, ex.Message, ex.StackTrace));
            return null;
        }
    }

    private async Task<Vrm10AnimationInstance> ImportVrmaAnimation(string fullPath, bool loop) {
        try {
            Debug.Log(string.Format(i18nMsg.LOG_VRMA_LOADING_START, fullPath));
            GltfData data = new AutoGltfFileParser(fullPath).Parse();
            if (data == null) {
                Debug.LogError(i18nMsg.ERROR_GLTFS_PARSE_FAILED);
                return null;
            }
            VrmAnimationImporter importer = new CustomVrmAnimationImporter(data);
            Debug.Log(string.Format(i18nMsg.LOG_VRMA_IMPORTER_CREATED, importer));
            var defaultMaterial = await importer.MaterialFactory.GetDefaultMaterialAsync(new ImmediateCaller());
            if (defaultMaterial == null) {
                Debug.LogError(i18nMsg.ERROR_MATERIAL_NULL);
                return null;
            }
            if (defaultMaterial.shader == null) {
                Debug.LogError(i18nMsg.ERROR_SHADER_NULL);
                return null;
            }
            Debug.Log(string.Format(i18nMsg.LOG_DEFAULT_MATERIAL, defaultMaterial.name, defaultMaterial.shader.name));
            var instance = await importer.LoadAsync(new ImmediateCaller());
            if (instance == null) {
                Debug.LogError(i18nMsg.ERROR_VRMA_INSTANCE_LOAD_FAILED);
                return null;
            }
            Vrm10AnimationInstance vrmaInstance = instance.GetComponent<Vrm10AnimationInstance>();
            if (vrmaInstance == null) {
                Debug.LogError(i18nMsg.ERROR_VRMA_INSTANCE_COMPONENT_MISSING);
                return null;
            }
            Debug.Log(string.Format(i18nMsg.LOG_VRMA_INSTANCE_SUCCESS, vrmaInstance));
            foreach (var renderer in vrmaInstance.GetComponentsInChildren<Renderer>()) {
                foreach (var mat in renderer.sharedMaterials) {
                    if (mat != null) {
                        Debug.Log(string.Format(i18nMsg.LOG_MATERIAL_LOADED, mat.name, (mat.shader != null ? mat.shader.name : "NULL")));
                    }
                }
            }
            return vrmaInstance;
        }
        catch (Exception ex) {
            Debug.LogError(string.Format(i18nMsg.ERROR_IMPORT_VRMA_EXCEPTION, ex.Message, ex.StackTrace));
            return null;
        }
    }

    /// <summary>
    /// VRMA インスタンス内の SkinnedMeshRenderer をすべて無効化して Box Man（デバッグ用ボーン）の表示を抑制する
    /// </summary>
    private void HideBoxManFromAnimationInstance(Vrm10AnimationInstance vrmaInstance) {
        if (vrmaInstance == null) {
            Debug.LogWarning(i18nMsg.WARNING_VRMA_INSTANCE_MISSING);
            return;
        }
        SkinnedMeshRenderer[] skinnedMeshes = vrmaInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var skinnedMesh in skinnedMeshes) {
            Debug.Log(string.Format(i18nMsg.LOG_SKINNEDMESH_DISABLED, skinnedMesh.name));
            skinnedMesh.enabled = false;
        }
    }

    private Dictionary<Transform, Quaternion> CaptureSpringBoneRotations(GameObject root)
    {
        var dict = new Dictionary<Transform, Quaternion>();
        if (root == null) return dict;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (var comp in t.GetComponents<Component>())
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;
                if (typeName.Contains("SpringBone"))
                {
                    dict[t] = t.localRotation;
                    break;
                }
            }
        }
        return dict;
    }

    private IEnumerator RestoreSpringBoneRotationsNextFrame(Dictionary<Transform, Quaternion> saved)
    {
        yield return null;
        foreach (var kv in saved)
        {
            if (kv.Key != null)
            {
                kv.Key.localRotation = kv.Value;
            }
        }
    }
}
