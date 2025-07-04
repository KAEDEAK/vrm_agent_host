using UnityEngine;
using UniVRM10;

/// <summary>
/// VRM10の視線制御を管理するコンポーネント
/// </summary>
public class VRM10LookAtController : MonoBehaviour
{
    private Vrm10Instance vrmInstance;
    private Vrm10RuntimeLookAt lookAt;
    
    // シングルトンインスタンス
    private static VRM10LookAtController currentInstance;
    
    void Start()
    {
        // VRM10Instanceを取得
        vrmInstance = GetComponent<Vrm10Instance>();
        if (vrmInstance == null)
        {
            Debug.LogError("[VRM10LookAtController] Vrm10Instance not found!");
            enabled = false;
            return;
        }
        
        currentInstance = this;
        Debug.Log("[VRM10LookAtController] Initialized");
    }
    
    void Update()
    {
        // Runtimeが初期化されているか確認
        if (vrmInstance != null && vrmInstance.Runtime != null)
        {
            lookAt = vrmInstance.Runtime.LookAt;
        }
    }
    
    /// <summary>
    /// 視線の角度を設定（Yaw/Pitch方式）
    /// </summary>
    /// <param name="yawDeg">水平方向の角度（度）。正の値は右、負の値は左</param>
    /// <param name="pitchDeg">垂直方向の角度（度）。正の値は上、負の値は下</param>
    public void SetLookRotation(float yawDeg, float pitchDeg)
    {
        if (lookAt == null)
        {
            Debug.LogWarning("[VRM10LookAtController] LookAt not initialized yet");
            return;
        }
        
        // LookAtTargetTypeをYawPitchValueに変更
        vrmInstance.LookAtTargetType = VRM10ObjectLookAt.LookAtTargetTypes.YawPitchValue;
        
        // Yaw/Pitchを設定
        lookAt.SetYawPitchManually(yawDeg, pitchDeg);
        
        Debug.Log($"[VRM10LookAtController] Set rotation - Yaw: {yawDeg}°, Pitch: {pitchDeg}°");
    }
    
    /// <summary>
    /// 特定のTransformを見るように設定
    /// </summary>
    /// <param name="target">見る対象のTransform</param>
    public void SetLookAtTarget(Transform target)
    {
        if (vrmInstance == null) return;
        
        // LookAtTargetTypeをSpecifiedTransformに変更
        vrmInstance.LookAtTargetType = VRM10ObjectLookAt.LookAtTargetTypes.SpecifiedTransform;
        vrmInstance.LookAtTarget = target;
        
        Debug.Log($"[VRM10LookAtController] Set target to: {target?.name ?? "null"}");
    }
    
    /// <summary>
    /// 視線をリセット（正面を向く）
    /// </summary>
    public void ResetLook()
    {
        SetLookRotation(0f, 0f);
        Debug.Log("[VRM10LookAtController] Reset to front");
    }
    
    /// <summary>
    /// 視線制御を無効化
    /// </summary>
    public void DisableLookAt()
    {
        if (vrmInstance == null) return;
        
        // LookAtTargetTypeをNoneに設定することで無効化
        vrmInstance.LookAtTargetType = VRM10ObjectLookAt.LookAtTargetTypes.YawPitchValue;
        SetLookRotation(0f, 0f);
        
        Debug.Log("[VRM10LookAtController] LookAt disabled");
    }
    
    /// <summary>
    /// 静的メソッド：現在のインスタンスに視線を設定
    /// </summary>
    public static void SetGlobalLookRotation(float yawDeg, float pitchDeg)
    {
        if (currentInstance != null)
        {
            currentInstance.SetLookRotation(yawDeg, pitchDeg);
        }
        else
        {
            Debug.LogWarning("[VRM10LookAtController] No active instance found");
        }
    }
    
    /// <summary>
    /// 静的メソッド：現在のインスタンスの視線ターゲットを設定
    /// </summary>
    public static void SetGlobalLookAtTarget(Transform target)
    {
        if (currentInstance != null)
        {
            currentInstance.SetLookAtTarget(target);
        }
        else
        {
            Debug.LogWarning("[VRM10LookAtController] No active instance found");
        }
    }
    
    void OnDestroy()
    {
        if (currentInstance == this)
        {
            currentInstance = null;
        }
    }
}
