using UnityEngine;
using System.Collections;

/// <summary>
/// アニメーションシステムの後で目の回転を強制的に上書きするコンポーネント
/// VRMモデルに自動的にアタッチされます
/// </summary>
public class EyeRotationOverride : MonoBehaviour {
    private Animator animator;
    private Transform leftEye;
    private Transform rightEye;

    // 目標の回転値
    private Quaternion targetLeftRotation = Quaternion.identity;
    private Quaternion targetRightRotation = Quaternion.identity;
    private bool hasTargetRotation = false;

    // 初期の目の向き（VRoid用）
    private Quaternion baseLeftRotation;
    private Quaternion baseRightRotation;
    private bool baseRotationCaptured = false;

    // シングルトンインスタンス
    private static EyeRotationOverride currentInstance;

    void Start() {
        StartCoroutine(InitializeAfterFrame());
    }

    IEnumerator InitializeAfterFrame() {
        // 1フレーム待機（アニメーションシステムの初期化を待つ）
        yield return null;

        animator = GetComponent<Animator>();
        if (animator != null) {
            leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);

            if (leftEye != null && rightEye != null) {
                // 初期の目の向きを記録（アニメーションの影響を受けた後の値）
                baseLeftRotation = leftEye.localRotation;
                baseRightRotation = rightEye.localRotation;
                baseRotationCaptured = true;

                Debug.Log($"[EyeOverride] Initialized - Base rotations captured: Left={baseLeftRotation.eulerAngles}, Right={baseRightRotation.eulerAngles}");
                currentInstance = this;
            } else {
                Debug.LogError("[EyeOverride] Eye bones not found!");
            }
        } else {
            Debug.LogError("[EyeOverride] Animator not found!");
        }
    }

    void LateUpdate() {
        // アニメーションの後で目の回転を上書き
        if (hasTargetRotation && leftEye != null && rightEye != null) {
            leftEye.localRotation = targetLeftRotation;
            rightEye.localRotation = targetRightRotation;
        }
    }

    /// <summary>
    /// 視線の角度を設定
    /// </summary>
    public void SetLookRotation(float yawDeg, float pitchDeg, string eye = "both") {
        if (!baseRotationCaptured) {
            Debug.LogWarning("[EyeOverride] Base rotation not captured yet");
            return;
        }

        // VRoidの座標系に合わせた調整
        Quaternion deltaRotation = Quaternion.Euler(-pitchDeg, yawDeg, 0f);

        if (eye == "both" || eye == "left") {
            targetLeftRotation = baseLeftRotation * deltaRotation;
        }
        if (eye == "both" || eye == "right") {
            targetRightRotation = baseRightRotation * deltaRotation;
        }

        hasTargetRotation = true;

        Debug.Log($"[EyeOverride] Set rotation - Yaw: {yawDeg}°, Pitch: {pitchDeg}°, Eye: {eye}");
    }

    /// <summary>
    /// 視線をリセット
    /// </summary>
    public void ResetLook() {
        if (baseRotationCaptured) {
            targetLeftRotation = baseLeftRotation;
            targetRightRotation = baseRightRotation;
            hasTargetRotation = true;
            Debug.Log("[EyeOverride] Reset to base rotation");
        }
    }

    /// <summary>
    /// 視線制御を無効化
    /// </summary>
    public void DisableOverride() {
        hasTargetRotation = false;
        Debug.Log("[EyeOverride] Override disabled");
    }

    /// <summary>
    /// 静的メソッド：現在のインスタンスに視線を設定
    /// </summary>
    public static void SetGlobalLookRotation(float yawDeg, float pitchDeg, string eye = "both") {
        if (currentInstance != null) {
            currentInstance.SetLookRotation(yawDeg, pitchDeg, eye);
        } else {
            Debug.LogWarning("[EyeOverride] No active instance found");
        }
    }

    void OnDestroy() {
        if (currentInstance == this) {
            currentInstance = null;
        }
    }
}

