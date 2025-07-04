using UnityEngine;
using UniVRM10;

/// <summary>
/// VRM10の視線制御をテストするためのスクリプト
/// </summary>
public class VRM10LookAtTest : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool enableTest = true;
    [SerializeField] private float testSpeed = 30f; // 度/秒
    [SerializeField] private float maxAngle = 30f;
    
    [Header("Test Target")]
    [SerializeField] private Transform testTarget;
    [SerializeField] private bool createTestTarget = true;
    [SerializeField] private float targetDistance = 2f;
    [SerializeField] private float targetHeight = 0f;
    
    private float currentYaw = 0f;
    private float currentPitch = 0f;
    private bool movingRight = true;
    private bool movingUp = true;
    
    void Start()
    {
        if (createTestTarget && testTarget == null)
        {
            // テスト用のターゲットオブジェクトを作成
            GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetObj.name = "LookAtTestTarget";
            targetObj.transform.localScale = Vector3.one * 0.1f;
            
            // マテリアルを赤色に設定
            var renderer = targetObj.GetComponent<Renderer>();
            renderer.material.color = Color.red;
            
            testTarget = targetObj.transform;
            UpdateTargetPosition();
        }
    }
    
    void Update()
    {
        if (!enableTest) return;
        
        // キーボード入力でテスト
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // スペースキーでリセット
            VRM10LookAtController.SetGlobalLookRotation(0f, 0f);
            currentYaw = 0f;
            currentPitch = 0f;
            Debug.Log("[VRM10LookAtTest] Reset look rotation");
        }
        
        if (Input.GetKeyDown(KeyCode.T))
        {
            // Tキーでターゲットモードに切り替え
            if (testTarget != null)
            {
                VRM10LookAtController.SetGlobalLookAtTarget(testTarget);
                Debug.Log("[VRM10LookAtTest] Switched to target mode");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.M))
        {
            // Mキーで手動モードに切り替え
            VRM10LookAtController.SetGlobalLookRotation(currentYaw, currentPitch);
            Debug.Log("[VRM10LookAtTest] Switched to manual mode");
        }
        
        // 矢印キーで手動制御
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            currentYaw -= testSpeed * Time.deltaTime;
            currentYaw = Mathf.Clamp(currentYaw, -maxAngle, maxAngle);
            VRM10LookAtController.SetGlobalLookRotation(currentYaw, currentPitch);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            currentYaw += testSpeed * Time.deltaTime;
            currentYaw = Mathf.Clamp(currentYaw, -maxAngle, maxAngle);
            VRM10LookAtController.SetGlobalLookRotation(currentYaw, currentPitch);
        }
        if (Input.GetKey(KeyCode.UpArrow))
        {
            currentPitch += testSpeed * Time.deltaTime;
            currentPitch = Mathf.Clamp(currentPitch, -maxAngle, maxAngle);
            VRM10LookAtController.SetGlobalLookRotation(currentYaw, currentPitch);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            currentPitch -= testSpeed * Time.deltaTime;
            currentPitch = Mathf.Clamp(currentPitch, -maxAngle, maxAngle);
            VRM10LookAtController.SetGlobalLookRotation(currentYaw, currentPitch);
        }
        
        // Aキーで自動テスト
        if (Input.GetKey(KeyCode.A))
        {
            AutoTest();
        }
        
        // ターゲットの位置を更新
        if (testTarget != null && Input.GetKey(KeyCode.LeftShift))
        {
            UpdateTargetPosition();
        }
    }
    
    void AutoTest()
    {
        // 自動的に視線を動かす
        if (movingRight)
        {
            currentYaw += testSpeed * Time.deltaTime;
            if (currentYaw >= maxAngle)
            {
                currentYaw = maxAngle;
                movingRight = false;
            }
        }
        else
        {
            currentYaw -= testSpeed * Time.deltaTime;
            if (currentYaw <= -maxAngle)
            {
                currentYaw = -maxAngle;
                movingRight = true;
            }
        }
        
        if (movingUp)
        {
            currentPitch += testSpeed * 0.5f * Time.deltaTime;
            if (currentPitch >= maxAngle * 0.5f)
            {
                currentPitch = maxAngle * 0.5f;
                movingUp = false;
            }
        }
        else
        {
            currentPitch -= testSpeed * 0.5f * Time.deltaTime;
            if (currentPitch <= -maxAngle * 0.5f)
            {
                currentPitch = -maxAngle * 0.5f;
                movingUp = true;
            }
        }
        
        VRM10LookAtController.SetGlobalLookRotation(currentYaw, currentPitch);
    }
    
    void UpdateTargetPosition()
    {
        if (testTarget == null) return;
        
        // マウスの位置に基づいてターゲットを配置
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = targetDistance;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        worldPos.y += targetHeight;
        testTarget.position = worldPos;
    }
    
    void OnGUI()
    {
        if (!enableTest) return;
        
        // デバッグ情報を表示
        GUI.Box(new Rect(10, 10, 300, 150), "VRM10 LookAt Test");
        GUI.Label(new Rect(20, 30, 280, 20), $"Yaw: {currentYaw:F1}° / Pitch: {currentPitch:F1}°");
        GUI.Label(new Rect(20, 50, 280, 20), "Controls:");
        GUI.Label(new Rect(20, 70, 280, 20), "Arrow Keys: Manual control");
        GUI.Label(new Rect(20, 90, 280, 20), "A: Auto test / Space: Reset");
        GUI.Label(new Rect(20, 110, 280, 20), "T: Target mode / M: Manual mode");
        GUI.Label(new Rect(20, 130, 280, 20), "Shift + Mouse: Move target");
    }
}
