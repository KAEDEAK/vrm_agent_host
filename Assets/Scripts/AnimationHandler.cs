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
using System.Threading;

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

    // AGIA playback lock counter
    private int agiaLockCount = 0;

    /// <summary>
    /// Returns true when built-in AGIA animations are playing
    /// (i.e. no external VRMA is active).
    /// </summary>
    public bool IsAgiaPlaying
    {
        get
        {
            return agiaLockCount > 0;
        }
    }

    /// <summary>
    /// Increment AGIA playback lock counter
    /// </summary>
    public void LockAGIA()
    {
        Interlocked.Increment(ref agiaLockCount);
    }

    /// <summary>
    /// Decrement AGIA playback lock counter
    /// </summary>
    public void UnlockAGIA()
    {
        if (Interlocked.Decrement(ref agiaLockCount) < 0)
        {
            Interlocked.Exchange(ref agiaLockCount, 0);
        }
    }

    /// <summary>
    /// Returns true when AGIA is locked (playing)
    /// </summary>
    public bool IsPlayingAGIA => agiaLockCount != 0;

    /// <summary>
    /// Get current AGIA lock count for debugging
    /// </summary>
    public int GetAgiaLockCount() => agiaLockCount;

    /// <summary>
    /// Force reset AGIA lock counter (emergency use only)
    /// </summary>
    public void ForceResetAgiaLock()
    {
        Debug.LogWarning($"[EMERGENCY] Force resetting AGIA lock counter from {agiaLockCount} to 0");
        Interlocked.Exchange(ref agiaLockCount, 0);
    }

    /// <summary>
    /// Called before loading a new VRM model to stop running coroutines
    /// and clear current animator reference.
    /// </summary>
    public void PrepareForVrmReload()
    {
        StopAllCoroutines();
        animator = null;
        vrmModel = null;
        isInitialized = false;
        currentVrmaInstance = null;
        agiaLockCount = 0;
    }

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
        
        // StateMachineBehaviourのイベントを購読
        AnimationStateBehaviour.OnAnimationEnter += OnAnimationStateEnter;
        AnimationStateBehaviour.OnAnimationExit += OnAnimationStateExit;
    }

    private void OnDestroy() {
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnModelLoaded;
        }
        onAnimationReady -= OnAnimationReady;
        
        // StateMachineBehaviourのイベントを解除
        AnimationStateBehaviour.OnAnimationEnter -= OnAnimationStateEnter;
        AnimationStateBehaviour.OnAnimationExit -= OnAnimationStateExit;
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
        Debug.Log($"[AnimationHandler] ForceStateAfterTransition started for {stateKey}, delay={delay}s, current lock count={agiaLockCount}");
        
        yield return new WaitForSeconds(delay);
        
        if (animator != null) {
            animator.Play(stateKey, layer, 0f);
            currentState = stateKey;
            Debug.Log(string.Format(i18nMsg.LOG_FORCE_STATE, stateKey, layer));
            
            // seamlessアニメーションの場合、追加の監視を開始（ロック解除はMonitorAnimationCompletionに任せる）
            StartCoroutine(MonitorAnimationCompletion(stateKey));
        }
        else {
            // animatorがnullの場合のみここでロック解除
            Debug.Log($"[AnimationHandler] ForceStateAfterTransition unlocking AGIA for {stateKey} (animator is null), lock count before unlock={agiaLockCount}");
            UnlockAGIA();
        }
        
        Debug.Log($"[AnimationHandler] ForceStateAfterTransition completed for {stateKey}, lock count now={agiaLockCount}");
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
                LockAGIA();
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
                LockAGIA();
                StartCoroutine(WaitForParameterUpdateAndPlay(stateName));
            }
        });
    }

    private IEnumerator WaitForParameterUpdateAndPlay(string stateName) {
        yield return null;
        animator.Play(stateName);
        currentState = stateName;
        Debug.Log(string.Format(i18nMsg.LOG_ANIMATION_PLAYED, stateName));
        StartCoroutine(MonitorAnimationCompletion(stateName));
    }

    // StateMachineBehaviourのイベントハンドラー
    private void OnAnimationStateEnter(string stateName)
    {
        Debug.Log($"[AnimationHandler] Animation state entered: {stateName}");
        currentState = stateName;
    }

    private void OnAnimationStateExit(string stateName)
    {
        Debug.Log($"[AnimationHandler] Animation state exited: {stateName}");
        
        // AGIAアニメーションの場合のみロックを解除
        if (IsAgiaAnimationState(stateName))
        {
            Debug.Log($"[AnimationHandler] AGIA animation completed: {stateName}, unlocking AGIA");
            UnlockAGIA();
        }
    }

    /// <summary>
    /// 指定されたステート名がAGIAアニメーションかどうかを判定
    /// </summary>
    private bool IsAgiaAnimationState(string stateName)
    {
        // AGIA関連のステート名パターンをチェック
        return stateName.StartsWith("Idle_") || 
               stateName.StartsWith("Other_") || 
               stateName.StartsWith("Layer_") ||
               stateName.StartsWith("ID_");
    }

    private IEnumerator MonitorAnimationCompletion(string stateName)
    {
        Debug.Log($"[AnimationHandler] Starting animation completion monitoring for: {stateName}");
        
        if (animator == null) 
        {
            Debug.LogError($"[AnimationHandler] Animator is null, unlocking immediately for: {stateName}");
            UnlockAGIA();
            yield break;
        }

        // アニメーションが開始されるまで少し待つ
        yield return new WaitForSeconds(0.1f);
        
        const float CHECK_INTERVAL = 1.5f; // 1.5秒間隔でチェック
        const float TIMEOUT_SECONDS = 30f; // 30秒でタイムアウト
        float timeoutCounter = 0f;
        
        while (timeoutCounter < TIMEOUT_SECONDS)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            
            // 現在のステート情報をログ出力
            Debug.Log($"[AnimationHandler] Monitoring {stateName}: current={info.shortNameHash}, normalizedTime={info.normalizedTime:F2}, isTransition={animator.IsInTransition(0)}");
            
            // アニメーションが完了した場合
            if (!info.IsName(stateName))
            {
                Debug.Log($"[AnimationHandler] Animation state changed from {stateName}, unlocking AGIA");
                break;
            }
            
            // アニメーションが1回再生完了し、遷移中でない場合
            if (info.normalizedTime >= 1f && !animator.IsInTransition(0))
            {
                Debug.Log($"[AnimationHandler] Animation {stateName} completed (normalizedTime={info.normalizedTime:F2}), unlocking AGIA");
                break;
            }
            
            // 1.5秒待機
            yield return new WaitForSeconds(CHECK_INTERVAL);
            timeoutCounter += CHECK_INTERVAL;
        }
        
        if (timeoutCounter >= TIMEOUT_SECONDS)
        {
            Debug.LogWarning($"[AnimationHandler] Animation monitoring timed out for {stateName}, force unlocking AGIA");
        }
        
        UnlockAGIA();
        Debug.Log($"[AnimationHandler] Animation monitoring completed for {stateName}, lock count now: {agiaLockCount}");
    }

    public string GetAnimationStatusJson() {
        var statusInfo = new Dictionary<string, object>();
        statusInfo["currentAnimation"] = currentState;
        statusInfo["availableStates"] = animationStates;
        statusInfo["isInitialized"] = isInitialized;
        statusInfo["isPlayingAGIA"] = IsPlayingAGIA;
        statusInfo["agiaLockCount"] = agiaLockCount;
        statusInfo["hasVrmaInstance"] = currentVrmaInstance != null;
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
        // 改良版メソッドへ委譲
        yield return StartCoroutine(ImprovedWaitForVrmaToFinish(vrmaInstance, nextState));
    }

    /// <summary>
    /// SpringBone を一時的に無効化してアニメータ切り替え時の物理リセットを防ぐ（改良版）
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
            List<Component> disabledJoints = DisableAllSpringBones(vrmLoader.VrmInstance.gameObject);
            Debug.Log("🔧 SpringBone disabled before animator transition");

            // 2. VRMAアニメーション解除
            if (vrmLoader.VrmInstance != null && vrmLoader.VrmInstance.Runtime != null)
            {
                vrmLoader.VrmInstance.Runtime.VrmAnimation = null;
            }
            yield return null; // 1フレーム待機

            // 3. 穏やかなアニメータ切り替え（Rebindを避ける）
            animator.runtimeAnimatorController = externalController;
            yield return null; // アニメータコントローラ設定の反映を待つ

            // 4. 次のアニメーション開始
            string targetState = string.IsNullOrEmpty(nextState) ? "Idle_generic_01" : nextState;
            Debug.Log($"🎯 Starting transition to: {targetState}");
            PlayAnimationByName(targetState);

            // 5. アニメーション安定まで待機
            yield return new WaitForSeconds(0.2f);

            // 6. SpringBone を段階的に復帰
            EnableSpringBones(disabledJoints);
            Debug.Log("✅ SpringBone re-enabled after seamless transition completed");
        }
        else
        {
            Debug.LogError("❌ ERROR: Animator is null, cannot switch animation.");
        }
    }

    /// <summary>
    /// SpringBone機能は廃止されました - 代替実装として空のメソッドを提供
    /// </summary>
    private List<Component> DisableAllSpringBones(GameObject root)
    {
        var disabled = new List<Component>();
        if (root == null) return disabled;

        // SpringBone機能は廃止されたため、何も行わない
        Debug.Log("🔧 SpringBone functionality has been deprecated - skipping disable operation");
        return disabled;
    }

    /// <summary>
    /// SpringBone機能は廃止されました - 代替実装として空のメソッドを提供
    /// </summary>
    private void EnableSpringBones(List<Component> joints)
    {
        // SpringBone機能は廃止されたため、何も行わない
        Debug.Log("🔧 SpringBone functionality has been deprecated - skipping enable operation");
    }

    /// <summary>
    /// SpringBone機能は廃止されました - 段階的無効化メソッドも廃止
    /// </summary>
    private IEnumerator GradualDisableSpringBones(GameObject root, float duration = 0.2f)
    {
        if (root == null) yield break;

        // SpringBone機能は廃止されたため、何も行わない
        Debug.Log("🔧 SpringBone functionality has been deprecated - skipping gradual disable operation");
        yield break;
    }

    /// <summary>
    /// SpringBone機能は廃止されました - ランダム無効化メソッドも廃止
    /// </summary>
    private IEnumerator RandomDisableSpringBones(GameObject root, float duration = 0.15f)
    {
        if (root == null) yield break;

        // SpringBone機能は廃止されたため、何も行わない
        Debug.Log("🔧 SpringBone functionality has been deprecated - skipping random disable operation");
        yield break;
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
        
        // まず既存のロックを強制リセット
        int oldLockCount = agiaLockCount;
        ForceResetAgiaLock();
        Debug.Log($"🔓 Force reset lock counter from {oldLockCount} to 0 before reset");

        // 1. SpringBone を事前に無効化
        List<Component> disabledJoints = DisableAllSpringBones(vrmLoader?.VrmInstance?.gameObject);

        // 2. VRMAアニメーション解除
        if (vrmLoader?.VrmInstance?.Runtime != null) {
            vrmLoader.VrmInstance.Runtime.VrmAnimation = null;
            Debug.Log(i18nMsg.LOG_VRMA_ANIMATION_RESET);
        }
        yield return null; // 1フレーム待機

        // 3. 穏やかなアニメータリセット（Rebindを避ける）
        if (animator != null) {
            animator.runtimeAnimatorController = externalController;
            yield return null; // アニメータコントローラ設定の反映を待つ

            // 4. デフォルトアニメーション開始
            PlayAnimationByName("Idle_generic_01");
            yield return new WaitForSeconds(0.2f); // アニメーション安定まで待機
        }

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
        LockAGIA();
        animator.Play(stateName, layer, normalizedTime);
        Debug.Log(string.Format(i18nMsg.LOG_ANIMATION_PLAYED, stateName));
        StartCoroutine(MonitorAnimationCompletion(stateName));
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
