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
        int layer = stateName.StartsWith("Layer_") ? 1 : 0;
        if (layer >= animator.layerCount || !animator.HasState(layer, Animator.StringToHash(stateName))) {
            Debug.LogWarning($"Animator state not found: {stateName} (layer {layer})");
            yield break;
        }
        animator.Play(stateName, layer, 0f);
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

    /// <summary>
    /// ボーンの位置・回転データを保存するための構造体
    /// </summary>
    [Serializable]
    public class TransformData
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public TransformData(Transform transform)
        {
            if (transform != null)
            {
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
            }
            else
            {
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                localScale = Vector3.one;
            }
        }

        public void ApplyTo(Transform transform)
        {
            if (transform != null)
            {
                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = localScale;
            }
        }
    }

    /// <summary>
    /// SpringBoneの状態を保存するクラス（簡略版）
    /// </summary>
    [Serializable]
    public class SpringBoneState
    {
        public VRM10SpringBoneJoint joint;
        public bool wasEnabled;

        public SpringBoneState(VRM10SpringBoneJoint springBoneJoint)
        {
            joint = springBoneJoint;
            if (joint != null)
            {
                wasEnabled = joint.enabled;
            }
        }

        public void Disable()
        {
            if (joint != null)
            {
                joint.enabled = false;
            }
        }

        public void RestoreOriginal()
        {
            if (joint != null)
            {
                joint.enabled = wasEnabled;
            }
        }
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
        // 改良版メソッドへ委譲
        yield return StartCoroutine(ImprovedWaitForVrmaToFinish(vrmaInstance, nextState));
    }

    /// <summary>
    /// SpringBone を一時的に無効化してアニメータ切り替え時の物理リセットを防ぐ（シームレス遷移版）
    /// </summary>
    private IEnumerator ImprovedWaitForVrmaToFinish(Vrm10AnimationInstance vrmaInstance, string nextState = "")
    {
        if (vrmaInstance == null) yield break;

        Animation anim = vrmaInstance.GetComponent<Animation>();
        if (anim == null || anim.clip == null)
        {
            Debug.LogWarning("⚠ VRMA animation component is missing.");
            yield break;
        }

        // VRMA アニメーションの終了を待つ
        yield return new WaitForSeconds(anim.clip.length);
        Debug.Log($"🎬 VRMA animation finished. Preparing seamless transition to {nextState}");

        if (animator != null)
        {
            // 1. SpringBone を事前に無効化（アニメータ切り替え前）
            List<VRM10SpringBoneJoint> disabledJoints = DisableAllSpringBones(vrmLoader.VrmInstance.gameObject);
            Debug.Log("🔧 SpringBone disabled before seamless transition");

            // 2. VRMAアニメーション解除
            if (vrmLoader.VrmInstance != null && vrmLoader.VrmInstance.Runtime != null)
            {
                vrmLoader.VrmInstance.Runtime.VrmAnimation = null;
            }
            yield return null; // 1フレーム待機

            // 3. アニメータコントローラ設定（Rebindを避ける）
            animator.runtimeAnimatorController = externalController;
            yield return null; // アニメータコントローラ設定の反映を待つ

            // 4. シームレス遷移の実行
            string targetState = string.IsNullOrEmpty(nextState) ? "Idle_generic_01" : nextState;
            yield return StartCoroutine(PerformSeamlessTransitionToBuiltinAnimation(targetState, disabledJoints));
        }
        else
        {
            Debug.LogError("❌ ERROR: Animator is null, cannot switch animation.");
        }
    }

    /// <summary>
    /// VRMAの最終ポーズからビルトインアニメーションへの手動補間による真のシームレス遷移を実行
    /// </summary>
    private IEnumerator PerformSeamlessTransitionToBuiltinAnimation(string targetState, List<VRM10SpringBoneJoint> disabledJoints)
    {
        Debug.Log($"🎯 Starting manual interpolation transition to: {targetState}");

        // 座標ログ: VRMA終了時
        LogModelCoordinates("VRMA終了時");

        // 1. VRMA終了時の現在ポーズをキャプチャ
        Dictionary<string, TransformData> vrmaEndPose = CaptureCurrentPose();
        Debug.Log($"📸 Captured VRMA end pose: {vrmaEndPose.Count} bones");

        // 2. アニメータコントローラ設定
        animator.runtimeAnimatorController = externalController;
        yield return null;

        // 3. ターゲットアニメーションを一時的に再生してポーズをキャプチャ
        Dictionary<string, TransformData> targetPose = null;
        yield return StartCoroutine(CaptureTargetAnimationPoseCoroutine(targetState, (result) => targetPose = result));
        if (targetPose == null || targetPose.Count == 0)
        {
            Debug.LogError("❌ Failed to capture target pose, falling back to direct transition");
            PlayAnimationByName(targetState);
            EnableSpringBones(disabledJoints);
            yield break;
        }

        Debug.Log($"📸 Captured target pose: {targetPose.Count} bones");

        // 座標ログ: ターゲットポーズキャプチャ後
        LogModelCoordinates("ターゲットポーズキャプチャ後");

        // 4. VRMA終了ポーズを復元（Hipsボーン座標を保護）
        Vector3 vrmaHipsPos = Vector3.zero;
        Transform hips = FindBoneByName("Hips");
        if (hips != null)
        {
            // VRMA終了時のHips座標を保存（ターゲットポーズキャプチャ前の値）
            if (vrmaEndPose.ContainsKey(hips.name))
            {
                vrmaHipsPos = vrmaEndPose[hips.name].localPosition;
            }
            else
            {
                vrmaHipsPos = hips.localPosition;
            }
        }
        
        // VRMA終了ポーズを復元（Hipsボーンを除外）
        ApplyPoseToModel(vrmaEndPose, excludeHips: true);
        
        // VRMA終了時のHips座標を強制復元
        if (hips != null)
        {
            hips.localPosition = vrmaHipsPos;
            Debug.Log($"🔧 VRMA終了時のHips座標を強制復元: {vrmaHipsPos}");
        }
        
        yield return null;

        // 座標ログ: VRMA終了ポーズ復元後
        LogModelCoordinates("VRMA終了ポーズ復元後");

        // 5. 手動補間実行
        float interpolationDuration = 1.0f; // 補間時間（1秒でより滑らかに）
        yield return StartCoroutine(InterpolateBetweenPoses(vrmaEndPose, targetPose, interpolationDuration));

        // 座標ログ: 補間完了後
        LogModelCoordinates("補間完了後");

        // 6. 最終的にアニメータに制御を移譲
        PlayAnimationByName(targetState);
        yield return new WaitForSeconds(0.5f); // アニメーション安定まで待機時間を延長

        // 座標ログ: アニメータ制御移譲後
        LogModelCoordinates("アニメータ制御移譲後");

        // 7. Hipsボーン座標の最終確認・固定
        Transform finalHips = FindBoneByName("Hips");
        if (finalHips != null && vrmaEndPose.ContainsKey(finalHips.name))
        {
            Vector3 originalHipsPos = vrmaEndPose[finalHips.name].localPosition;
            finalHips.localPosition = originalHipsPos;
            Debug.Log($"🔒 最終Hips座標確認・固定: {originalHipsPos}");
        }

        // 8. SpringBone段階的復帰
        yield return StartCoroutine(GradualEnableSpringBones(disabledJoints, 0.3f));
        Debug.Log("✅ Manual interpolation transition completed with gradual SpringBone restoration");
    }

    /// <summary>
    /// 現在のモデルのポーズをキャプチャする
    /// </summary>
    private Dictionary<string, TransformData> CaptureCurrentPose()
    {
        var pose = new Dictionary<string, TransformData>();
        if (vrmModel == null) return pose;

        // 主要なボーンのみキャプチャ（パフォーマンス考慮）
        var transforms = vrmModel.GetComponentsInChildren<Transform>();
        foreach (var transform in transforms)
        {
            // ボーン名でフィルタリング（VRMの主要ボーンのみ）
            if (IsImportantBone(transform.name))
            {
                pose[transform.name] = new TransformData(transform);
            }
        }

        return pose;
    }

    /// <summary>
    /// 指定されたアニメーションのポーズをキャプチャする（コールバック版）
    /// </summary>
    private IEnumerator CaptureTargetAnimationPoseCoroutine(string targetState, System.Action<Dictionary<string, TransformData>> callback)
    {
        // アニメーションIDとカテゴリを取得
        string category = "Idle";
        if (targetState.StartsWith("Other_"))
            category = "Other";
        else if (targetState.StartsWith("Layer_"))
            category = "Layer";

        string animationKey = targetState.Replace(category + "_", "").ToLower();
        animationKey = animationKey.Split('_')[0];
        int animationID = GetAnimationID(category, animationKey);

        if (animationID == -1)
        {
            Debug.LogError($"❌ Invalid animation ID for {targetState}");
            callback?.Invoke(null);
            yield break;
        }

        // アニメータパラメータ設定
        if (category == "Idle")
        {
            animator.SetInteger("animBaseInt", animationID);
        }
        else if (category == "Other")
        {
            animator.SetInteger("animOtherInt", animationID);
        }

        // アニメーション開始
        var config = ServerConfig.Instance;
        string animKey = config.GetAnimationName(category, animationID);
        string stateKey = category + "_" + (animKey.Contains("_") ? animKey : animKey + "_01");
        int layer = (category == "Layer") ? 1 : 0;

        animator.Play(stateKey, layer, 0f);
        yield return null; // 1フレーム待機してポーズを安定化

        // ポーズキャプチャ
        var targetPose = CaptureCurrentPose();
        callback?.Invoke(targetPose);
    }

    /// <summary>
    /// 2つのポーズ間を手動補間する（Hipsボーン座標固定版）
    /// </summary>
    private IEnumerator InterpolateBetweenPoses(Dictionary<string, TransformData> startPose, Dictionary<string, TransformData> endPose, float duration)
    {
        Debug.Log($"🔄 Starting pose interpolation (duration: {duration}s)");

        float elapsed = 0f;
        var interpolatedPose = new Dictionary<string, TransformData>();

        // Hipsボーンの固定座標を取得
        Transform hips = FindBoneByName("Hips");
        Vector3 fixedHipsPosition = Vector3.zero;
        if (hips != null && startPose.ContainsKey(hips.name))
        {
            fixedHipsPosition = startPose[hips.name].localPosition;
            Debug.Log($"🔒 Hips座標を固定: {fixedHipsPosition}");
        }

        // 共通のボーンのみ補間（Hipsを除外）
        var commonBones = new List<string>();
        foreach (var boneName in startPose.Keys)
        {
            if (endPose.ContainsKey(boneName) && !boneName.Contains("Hips"))
            {
                commonBones.Add(boneName);
            }
        }

        Debug.Log($"🦴 Interpolating {commonBones.Count} common bones (excluding Hips)");

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // イージング関数適用（スムーズな遷移）
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // 各ボーンを補間（Hipsを除く）
            foreach (var boneName in commonBones)
            {
                var startData = startPose[boneName];
                var endData = endPose[boneName];

                var interpolatedData = new TransformData(null)
                {
                    localPosition = Vector3.Lerp(startData.localPosition, endData.localPosition, smoothT),
                    localRotation = Quaternion.Slerp(startData.localRotation, endData.localRotation, smoothT),
                    localScale = Vector3.Lerp(startData.localScale, endData.localScale, smoothT)
                };

                interpolatedPose[boneName] = interpolatedData;
            }

            // 補間されたポーズを適用（Hipsを除外）
            ApplyPoseToModel(interpolatedPose, excludeHips: true);
            
            // Hipsボーン座標を強制固定
            if (hips != null)
            {
                hips.localPosition = fixedHipsPosition;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 最終ポーズを確実に適用（Hipsを除外）
        ApplyPoseToModel(endPose, excludeHips: true);
        
        // Hipsボーン座標を最終確認・固定
        if (hips != null)
        {
            hips.localPosition = fixedHipsPosition;
            Debug.Log($"🔒 最終Hips座標固定完了: {fixedHipsPosition}");
        }
        
        Debug.Log("✅ Pose interpolation completed with Hips position locked");
    }

    /// <summary>
    /// ポーズをモデルに適用する（Hipsボーンを除外可能）
    /// </summary>
    private void ApplyPoseToModel(Dictionary<string, TransformData> pose, bool excludeHips = false)
    {
        if (vrmModel == null) return;

        var transforms = vrmModel.GetComponentsInChildren<Transform>();
        foreach (var transform in transforms)
        {
            if (pose.ContainsKey(transform.name))
            {
                // Hipsボーン除外オプション
                if (excludeHips && transform.name.Contains("Hips"))
                {
                    continue;
                }
                pose[transform.name].ApplyTo(transform);
            }
        }
    }

    /// <summary>
    /// 重要なボーンかどうかを判定する（パフォーマンス最適化）
    /// </summary>
    private bool IsImportantBone(string boneName)
    {
        // VRMの主要ボーンのみを対象とする（Hipsも含めてキャプチャ）
        string[] importantBones = {
            "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
            "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"
        };

        foreach (var important in importantBones)
        {
            if (boneName.Contains(important))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 全ての SpringBoneJoint を一時的に無効化し、無効化したリストを返す（改良版）
    /// </summary>
    private List<VRM10SpringBoneJoint> DisableAllSpringBones(GameObject root)
    {
        var disabled = new List<VRM10SpringBoneJoint>();
        if (root == null) return disabled;

        // 1. VRMインスタンスから直接SpringBoneを取得
        if (vrmLoader?.VrmInstance?.SpringBone != null)
        {
            var springBoneManager = vrmLoader.VrmInstance.SpringBone;
            foreach (var spring in springBoneManager.Springs)
            {
                foreach (var joint in spring.Joints)
                {
                    if (joint != null && joint.enabled)
                    {
                        joint.enabled = false;
                        disabled.Add(joint);
                    }
                }
            }
            Debug.Log($"🔧 SpringBone disabled via VRM Instance API: {disabled.Count} joints");
        }
        else
        {
            // 2. フォールバック: GetComponentsInChildrenを使用
            var joints = root.GetComponentsInChildren<VRM10SpringBoneJoint>(true);
            foreach (var joint in joints)
            {
                if (joint.enabled)
                {
                    joint.enabled = false;
                    disabled.Add(joint);
                }
            }
            Debug.Log($"🔧 SpringBone disabled via GetComponentsInChildren: {disabled.Count} joints");
        }

        // SpringBone情報をログ出力
        LogSpringBoneInfo();
        return disabled;
    }

    /// <summary>
    /// SpringBone情報をデバッグログに出力する
    /// </summary>
    private void LogSpringBoneInfo()
    {
        if (vrmLoader?.VrmInstance?.SpringBone != null)
        {
            var springBoneManager = vrmLoader.VrmInstance.SpringBone;
            int totalJoints = 0;
            int enabledJoints = 0;

            foreach (var spring in springBoneManager.Springs)
            {
                totalJoints += spring.Joints.Count;
                foreach (var joint in spring.Joints)
                {
                    if (joint != null && joint.enabled)
                    {
                        enabledJoints++;
                    }
                }
            }

            Debug.Log($"🔍 SpringBone Info: {springBoneManager.Springs.Count} springs, {totalJoints} total joints, {enabledJoints} enabled joints");
        }
        else
        {
            Debug.Log("🔍 SpringBone Info: SpringBone manager not found");
        }
    }

    /// <summary>
    /// 指定された SpringBoneJoint を再有効化する
    /// </summary>
    private void EnableSpringBones(List<VRM10SpringBoneJoint> joints)
    {
        foreach (var joint in joints)
        {
            if (joint != null)
            {
                joint.enabled = true;
            }
        }

        Debug.Log($"🔧 SpringBone re-enabled: {joints.Count} joints");
    }

    /// <summary>
    /// SpringBone を段階的に無効化するオプションメソッド
    /// </summary>
    private IEnumerator GradualDisableSpringBones(GameObject root, float duration = 0.2f)
    {
        if (root == null) yield break;

        var joints = root.GetComponentsInChildren<VRM10SpringBoneJoint>(true);
        if (joints.Length == 0) yield break;

        float timePerStep = duration / joints.Length;
        foreach (var joint in joints)
        {
            if (joint.enabled)
            {
                joint.enabled = false;
                yield return new WaitForSeconds(timePerStep);
            }
        }

        Debug.Log($"🔧 SpringBone gradually disabled: {joints.Length} joints over {duration}s");
    }

    /// <summary>
    /// SpringBone をランダムな順序で無効化するオプションメソッド
    /// </summary>
    private IEnumerator RandomDisableSpringBones(GameObject root, float duration = 0.15f)
    {
        if (root == null) yield break;

        var joints = new List<VRM10SpringBoneJoint>(root.GetComponentsInChildren<VRM10SpringBoneJoint>(true));
        if (joints.Count == 0) yield break;

        // シャッフル
        for (int i = 0; i < joints.Count; i++)
        {
            var temp = joints[i];
            int randomIndex = UnityEngine.Random.Range(i, joints.Count);
            joints[i] = joints[randomIndex];
            joints[randomIndex] = temp;
        }

        float timePerStep = duration / joints.Count;
        foreach (var joint in joints)
        {
            if (joint.enabled)
            {
                joint.enabled = false;
                yield return new WaitForSeconds(timePerStep);
            }
        }

        Debug.Log($"🔧 SpringBone randomly disabled: {joints.Count} joints over {duration}s");
    }

    /// <summary>
    /// SpringBone を段階的に有効化する（物理リセット防止）
    /// </summary>
    private IEnumerator GradualEnableSpringBones(List<VRM10SpringBoneJoint> joints, float duration = 0.3f)
    {
        if (joints == null || joints.Count == 0) yield break;

        Debug.Log($"🔧 Starting gradual SpringBone enable: {joints.Count} joints over {duration}s");

        float timePerStep = duration / joints.Count;
        int enabledCount = 0;

        foreach (var joint in joints)
        {
            if (joint != null)
            {
                joint.enabled = true;
                enabledCount++;
                yield return new WaitForSeconds(timePerStep);
            }
        }

        Debug.Log($"🔧 SpringBone gradually enabled: {enabledCount} joints over {duration}s");
    }

    public void ResetAGIAAnimation() {
        if (animator == null) {
            Debug.LogError(i18nMsg.ERROR_ANIMATOR_NOT_SET);
            return;
        }

        // SpringBone を一時的に無効化してからリセット処理を実行
        StartCoroutine(ResetAGIAAnimationWithSpringBoneProtection());
    }

    /// <summary>
    /// SpringBone保護付きのAGIAアニメーションリセット
    /// </summary>
    private IEnumerator ResetAGIAAnimationWithSpringBoneProtection()
    {
        Debug.Log("🔄 Starting AGIA animation reset with SpringBone protection");

        // 1. SpringBone を事前に無効化
        List<VRM10SpringBoneJoint> disabledJoints = DisableAllSpringBones(vrmLoader.VrmInstance.gameObject);

        // 2. VRMAアニメーション解除
        if (vrmLoader.VrmInstance != null && vrmLoader.VrmInstance.Runtime != null) {
            vrmLoader.VrmInstance.Runtime.VrmAnimation = null;
            Debug.Log(i18nMsg.LOG_VRMA_ANIMATION_RESET);
        }
        yield return null; // 1フレーム待機

        // 3. 穏やかなアニメータリセット（Rebindを避ける）
        animator.runtimeAnimatorController = externalController;
        yield return null; // アニメータコントローラ設定の反映を待つ

        // 4. デフォルトアニメーション開始
        PlayAnimationByName("Idle_generic_01");
        yield return new WaitForSeconds(0.2f); // アニメーション安定まで待機

        // 5. SpringBone復帰
        EnableSpringBones(disabledJoints);
        Debug.Log(i18nMsg.LOG_AGIA_RESET);
        Debug.Log("✅ AGIA reset completed with SpringBone protection");
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

    /// <summary>
    /// モデルの座標情報をログ出力する（デバッグ用）
    /// </summary>
    private void LogModelCoordinates(string phase)
    {
        if (vrmModel == null)
        {
            Debug.Log($"📍 {phase}: VRMモデルがnull");
            return;
        }

        // VRMモデル全体の座標
        Vector3 modelPos = vrmModel.transform.position;
        Vector3 modelLocalPos = vrmModel.transform.localPosition;
        
        Debug.Log($"📍 {phase}: VRMモデル座標 - World: {modelPos}, Local: {modelLocalPos}");

        // Hipsボーンの座標を探す
        Transform hips = FindBoneByName("Hips");
        if (hips != null)
        {
            Vector3 hipsPos = hips.position;
            Vector3 hipsLocalPos = hips.localPosition;
            Debug.Log($"📍 {phase}: Hipsボーン座標 - World: {hipsPos}, Local: {hipsLocalPos}");
        }
        else
        {
            Debug.Log($"📍 {phase}: Hipsボーンが見つかりません");
        }

        // Spineボーンの座標も確認
        Transform spine = FindBoneByName("Spine");
        if (spine != null)
        {
            Vector3 spinePos = spine.position;
            Vector3 spineLocalPos = spine.localPosition;
            Debug.Log($"📍 {phase}: Spineボーン座標 - World: {spinePos}, Local: {spineLocalPos}");
        }
    }

    /// <summary>
    /// 指定された名前のボーンを検索する
    /// </summary>
    private Transform FindBoneByName(string boneName)
    {
        if (vrmModel == null) return null;

        var transforms = vrmModel.GetComponentsInChildren<Transform>();
        foreach (var transform in transforms)
        {
            if (transform.name.Contains(boneName))
            {
                return transform;
            }
        }
        return null;
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
