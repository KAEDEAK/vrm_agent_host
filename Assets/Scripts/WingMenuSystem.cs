using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WingMenuSystem : MonoBehaviour
{
    // メニューアイテムの構造体
    [System.Serializable]
    public class WingMenuItem
    {
        public GameObject wingObject;
        public string label;
        public System.Action onClick;
        public Vector3 targetPosition;
        public Quaternion targetRotation;
    }

    // 設定
    [Header("Menu Settings")]
    private float wingScale = 0.5f;
    private float menuRadius = 2.0f;
    private float animationDuration = 0.3f;
    private float menuDistance = 3.0f; // カメラからの距離
    private Color wingColor = new Color(0.3f, 0.7f, 1f, 1f);
    private Color hoverColor = new Color(1f, 1f, 0.3f, 1f);
    private Color exitColor = new Color(1f, 0.3f, 0.3f, 1f);
    
    [Header("Rainbow Wing Settings")]
    [SerializeField] private bool enableRainbowEffect = true;
    [SerializeField] private float rainbowSpeed = 15.0f;
    [SerializeField] private float wingTransparency = 0.6f;
    [SerializeField] private float brightnessMultiplier = 2.2f;
    [SerializeField] private float saturation = 0.6f;
    
    [Header("Sparkle Particle Settings")]
    [SerializeField] private bool enableSparkleEffect = true;
    [SerializeField] private int particlesPerWing = 2;
    [SerializeField] private float particleLifetime = 1.0f;
    [SerializeField] private float particleSpawnInterval = 0.2f;
    [SerializeField] private float particleSize = 0.05f;
    
    // 虹色エフェクトの定数
    private const float RAINBOW_SPEED_MULTIPLIER = 1.6f;  // 色変化の速度倍率（1秒間に8回循環）

    // パーティクルデータ構造
    [System.Serializable]
    public class SparkleParticle
    {
        public GameObject particleObject;
        public float lifetime;
        public float maxLifetime;
        public Vector3 velocity;
        public float startAlpha;
        public int wingIndex;
    }

    // 内部状態
    private bool isMenuOpen = false;
    private bool isAnimating = false;
    private List<WingMenuItem> wingItems = new List<WingMenuItem>();
    private GameObject menuContainer;
    private VRMLoader vrmLoader;
    private Camera mainCamera;
    private int hoveredIndex = -1;
    private bool hasProcessedClick = false;
    private bool hasEverBeenClosed = false;  // メニューが一度でも閉じられたかを記録
    private float lastClickTime = 0f;
    private const float doubleClickThreshold = 0.3f;  // ダブルクリックの判定時間

    // パーティクルシステム
    private List<SparkleParticle> activeParticles = new List<SparkleParticle>();
    private float lastParticleSpawnTime = 0f;

    // レイヤー設定
    private const string MENU_LAYER_NAME = "UI";
    private int menuLayer;

    // HTTP制御用の新規プロパティ
    [Header("HTTP Control Settings")]
    private string[] menuLabels = new string[8]; // ラベル配列（羽には表示しない）
    private bool leftWingsVisible = true;
    private bool rightWingsVisible = true;
    private int leftWingCount = 4;
    private int rightWingCount = 4;
    private float angleDelta = 20f; // 羽の角度変化率
    private float angleStart = 0f;  // 羽の開始角度
    
    [Header("Color Control Settings")]
    private string normalColorMode = "white";        // 通常時の色モード
    private string animationColorMode = "gaming";     // アニメーション時の色モード  
    private string hoverNoCommandColorMode = "blue"; // ホバー時（コマンド無）の色モード
    private string hoverWithCommandColorMode = "yellow";  // ホバー時（コマンド有）の色モード
    
    // 色変換用の辞書
    private static readonly Dictionary<string, Color> ColorModeMap = new Dictionary<string, Color>
    {
        {"white", Color.white},
        {"lightblue", new Color(0.7f, 0.9f, 1f, 1f)},
        {"yellow", Color.yellow},
        {"red", Color.red},
        {"green", Color.green},
        {"blue", Color.blue},
        {"black", Color.black},
        {"gaming", Color.white} // ゲーミング効果用の特別値
    };
    
    [Header("Wing Shape Settings")]
    private float bladeLength = 1.0f;     // 羽の長さ（両側共通）
    private float bladeEdge = 0.5f;       // 形状の減衰率（両側共通）
    private float bladeModifier = 0.0f;   // 次の羽のサイズ減少率（両側共通）
    
    [Header("Wing Shape Settings - Left/Right Independent")]
    private float bladeLeftLength = 1.0f;     // 左側の羽の長さ
    private float bladeLeftEdge = 0.5f;       // 左側の形状の減衰率
    private float bladeLeftModifier = 0.0f;   // 左側の次の羽のサイズ減少率
    private float bladeRightLength = 1.0f;    // 右側の羽の長さ
    private float bladeRightEdge = 0.5f;      // 右側の形状の減衰率
    private float bladeRightModifier = 0.0f;  // 右側の次の羽のサイズ減少率
    private bool useIndependentShapes = true; // 左右独立設定を使用するかどうか（デフォルト: true）
    
    // blade_splitモードの定義
    public enum BladeSplitMode
    {
        Reset,  // 左右独立（blade_split=true相当）
        Split,  // 左右リセットされつつも0-360度配置
        Keep    // 連続（blade_split=false相当）
    }
    private BladeSplitMode bladeSplitMode = BladeSplitMode.Reset; // デフォルト: Reset

    void Start()
    {
        // コンポーネント取得
        vrmLoader = FindObjectOfType<VRMLoader>();
        mainCamera = Camera.main;
        
        // カメラの設定確認
        if (mainCamera != null)
        {
            Debug.Log($"[WingMenu] Camera found: {mainCamera.name}");
            Debug.Log($"[WingMenu] Camera culling mask: {mainCamera.cullingMask}");
        }
        else
        {
            Debug.LogError("[WingMenu] Main camera not found!");
        }
        
        // レイヤー設定
        menuLayer = LayerMask.NameToLayer(MENU_LAYER_NAME);
        if (menuLayer == -1)
        {
            Debug.LogError($"[WingMenu] Layer '{MENU_LAYER_NAME}' not found! Using default layer.");
            menuLayer = 0; // Default layer
        }
        else
        {
            Debug.Log($"[WingMenu] Using layer: {menuLayer} ({MENU_LAYER_NAME})");
        }
        
        // メニューコンテナ作成
        CreateMenuContainer();
        
        // 羽メニューアイテムを作成
        CreateWingItems();
        
        // VRMLoader の OnVRMLoadComplete イベントにメニュー調整を登録
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete += OnVrmModelLoaded;
        }
        
        // 初期状態では非表示
        HideMenuImmediate();
    }

    private void OnDestroy() {
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnVrmModelLoaded;
        }
    }

    void Update()
    {
        // 色制御システムの更新（常に実行）
        if (isMenuOpen)
        {
            UpdateWingColors();
        }

        // パーティクルエフェクトの更新
        if (enableSparkleEffect && isMenuOpen)
        {
            UpdateSparkleParticles();
            SpawnSparkleParticles();
        }

        // アニメーション中は入力を受け付けない
        if (isAnimating) return;

        // 左クリック処理（メニューアイテムのクリックのみ処理）
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[WingMenu] Mouse button down detected");
            HandleMenuItemClick();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            // マウスボタンが離されたらフラグをリセット
            hasProcessedClick = false;
        }
        
        // 右クリックでメニューのトグル
        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("[WingMenu] Right click - toggling menu");
            ToggleMenu();
        }

        // ホバー処理
        HandleHover();

        // 初回表示：アバターがない時、またはアバターが読み込まれた直後
        if (!isMenuOpen && !hasEverBeenClosed)
        {
            // 羽だけを画面中央に表示
            ShowMenuAtCenter();
        }
        
        // VRM読み込み前：メニューが閉じられてもすぐに再表示（終了ボタンアクセス確保）
        if (!isMenuOpen && hasEverBeenClosed && (vrmLoader == null || vrmLoader.LoadedModel == null))
        {
            // VRM読み込み前は強制的にメニューを再表示
            ShowMenuAtCenter();
            hasEverBeenClosed = false; // フラグをリセットして再表示を許可
        }
        
    // デバッグ用：Tキーでメニューを強制表示/非表示
    if (Input.GetKeyDown(KeyCode.T))
    {
        Debug.Log("[WingMenu] T key pressed - forcing menu toggle for debug");
        if (isMenuOpen)
        {
            HideMenu();
        }
        else
        {
            ShowMenu();
            // デバッグ情報も出力
            StartCoroutine(DelayedDebugInfo());
        }
    }
    
    // デバッグ用：Uキーで大きなテストメニューを表示
    if (Input.GetKeyDown(KeyCode.U))
    {
        Debug.Log("[WingMenu] U key pressed - showing large test menu");
        ShowLargeTestMenu();
    }
        
        // デバッグ用：Yキーでデバッグ情報を出力
        if (Input.GetKeyDown(KeyCode.Y))
        {
            Debug.Log("[WingMenu] Y key pressed - outputting debug info");
            DebugMenuVisibility();
        }
    }

    private void CreateMenuContainer()
    {
        menuContainer = new GameObject("WingMenuContainer");
        menuContainer.transform.SetParent(transform);
        // コンテナ全体のスケールを1,1,1に設定
        menuContainer.transform.localScale = Vector3.one;
    }

    private void CreateWingItems()
    {
        // 左側の羽を4個作成（対称配置：exit, reset_shape, reset_pose, placeholder）
        for (int i = 0; i < 4; i++)
        {
            var wingItem = new WingMenuItem();
            
            // 羽のGameObject作成
            wingItem.wingObject = CreateWingMesh($"Wing_Left_{i + 1}");
            wingItem.wingObject.transform.SetParent(menuContainer.transform);
            
            // 位置と回転を計算
            float angle = GetWingAngle(i, true);
            float radius = menuRadius;
            
            // 左側：下から上へ
            wingItem.targetPosition = new Vector3(
                -Mathf.Cos(angle) * radius - 0.5f,  // 左翼全体を左にオフセット
                -Mathf.Sin(angle) * radius * 0.8f,
                0.0f  // Z座標はメニューコンテナ基準で0（コンテナ自体がカメラ前面に配置される）
            );
            wingItem.targetRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 45);
            
            // 機能割り当て（対称配置）
            if (i == 0) // 左下（左側の1番目）がEXIT
            {
                wingItem.label = "exit";
                wingItem.onClick = OnExitClick;
            }
            else if (i == 1) // 左側の2番目がRESET_SHAPE
            {
                wingItem.label = "reset_shape";
                wingItem.onClick = () => ExecuteBuiltinFunction("reset_shape");
            }
            else if (i == 2) // 左側の3番目がRESET_POSE
            {
                wingItem.label = "reset_pose";
                wingItem.onClick = () => ExecuteBuiltinFunction("reset_pose");
            }
            else // 左側の4番目はプレースホルダー
            {
                wingItem.label = "placeholder";
                int capturedIndex = i;  // ラムダ式用にインデックスをキャプチャ
                wingItem.onClick = () => OnPlaceholderClick(capturedIndex);
            }
            
            wingItems.Add(wingItem);
        }
        
        // 右側の羽を4個作成（対称配置：exit, reset_shape, reset_pose, placeholder）
        for (int i = 0; i < 4; i++)
        {
            var wingItem = new WingMenuItem();
            
            // 羽のGameObject作成
            wingItem.wingObject = CreateWingMesh($"Wing_Right_{i + 1}");
            wingItem.wingObject.transform.SetParent(menuContainer.transform);
            
            // 位置と回転を計算
            float angle = GetWingAngle(i, false);
            float radius = menuRadius;
            
            // 右側：下から上へ
            wingItem.targetPosition = new Vector3(
                Mathf.Cos(angle) * radius + 0.5f,   // 右翼全体を右にオフセット
                -Mathf.Sin(angle) * radius * 0.8f,
                0.0f  // Z座標はメニューコンテナ基準で0（コンテナ自体がカメラ前面に配置される）
            );
            wingItem.targetRotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg + 45);
            
            // 機能割り当て（対称配置）
            if (i == 0) // 右下（右側の1番目）がEXIT
            {
                wingItem.label = "exit";
                wingItem.onClick = OnExitClick;
            }
            else if (i == 1) // 右側の2番目がRESET_SHAPE
            {
                wingItem.label = "reset_shape";
                wingItem.onClick = () => ExecuteBuiltinFunction("reset_shape");
            }
            else if (i == 2) // 右側の3番目がRESET_POSE
            {
                wingItem.label = "reset_pose";
                wingItem.onClick = () => ExecuteBuiltinFunction("reset_pose");
            }
            else // 右側の4番目はプレースホルダー
            {
                wingItem.label = "placeholder";
                int capturedIndex = i + 4;  // ラムダ式用にインデックスをキャプチャ
                wingItem.onClick = () => OnPlaceholderClick(capturedIndex);
            }
            
            wingItems.Add(wingItem);
        }
    }

    private float GetWingAngle(int index, bool isLeft)
    {
        // 羽の配置角度を計算（天使の羽のように下から上へ広がる）
        // 左右共通：下から上へ（-30度から90度）
        return Mathf.Lerp(-30f, 90f, index / 3f) * Mathf.Deg2Rad;
    }

    private GameObject CreateWingMesh(string name)
    {
        GameObject wing = new GameObject(name);
        wing.layer = menuLayer;
        
        // メッシュフィルター追加
        MeshFilter meshFilter = wing.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = wing.AddComponent<MeshRenderer>();
        
        // 羽の形状を作成（両面表示対応）
        Mesh mesh = new Mesh();
        
        // 頂点（羽の形に変形した四角形 - より羽らしい形状に）
        Vector3[] vertices = new Vector3[]
        {
            // 表面
            new Vector3(-0.2f, -0.6f, 0),  // 左下（根元）
            new Vector3(0.2f, -0.6f, 0),   // 右下（根元）
            new Vector3(0.3f, 0.5f, 0),    // 右上（先端）
            new Vector3(-0.3f, 0.5f, 0),   // 左上（先端）
            // 裏面（同じ頂点を複製）
            new Vector3(-0.2f, -0.6f, 0),  // 左下（根元）
            new Vector3(0.2f, -0.6f, 0),   // 右下（根元）
            new Vector3(0.3f, 0.5f, 0),    // 右上（先端）
            new Vector3(-0.3f, 0.5f, 0)    // 左上（先端）
        };
        
        // 三角形（表面と裏面の両方）
        int[] triangles = new int[] { 
            // 表面
            0, 2, 1, 0, 3, 2,
            // 裏面（逆順）
            4, 5, 6, 4, 6, 7
        };
        
        // UV座標
        Vector2[] uv = new Vector2[]
        {
            // 表面
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            // 裏面
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
        
        // 虹色エフェクト対応のマテリアル作成
        Material material = CreateRainbowMaterial(name);
        if (material == null)
        {
            Debug.LogError("[WingMenu] Failed to create material!");
            return null;
        }
        
        meshRenderer.material = material;
        
        // VRM読み込み後の場合は、作成直後にEmissionを適用
        if (vrmLoader != null && vrmLoader.LoadedModel != null)
        {
            ApplyBrightnessEmission(material);
        }
        
        // レンダリング順序を強制的に設定
        meshRenderer.sortingOrder = 100; // 高い値で前面に表示
        
        // デバッグ情報
        Debug.Log($"[WingMenu] Created wing: {name}, layer: {wing.layer} ({LayerMask.LayerToName(wing.layer)})");
        
        // コライダー追加（クリック検出用）
        BoxCollider collider = wing.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.6f * wingScale, 1.0f * wingScale, 0.1f);
        
        return wing;
    }

    private void HandleMenuItemClick()
    {
        // メニューが開いていない場合は何もしない
        if (!isMenuOpen) {
            hasProcessedClick = false;
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        Debug.Log("[WingMenu] Menu is open, checking wing clicks");
        
        // 羽のクリックチェック
        if (Physics.Raycast(ray, out hit, 100f, 1 << menuLayer))
        {
            Debug.Log($"[WingMenu] Hit object: {hit.collider.gameObject.name}");
            
            // 羽がクリックされた場合、即座にフラグを設定
            hasProcessedClick = true;
            
            for (int i = 0; i < wingItems.Count; i++)
            {
                if (hit.collider.gameObject == wingItems[i].wingObject)
                {
                    Debug.Log($"[WingMenu] Wing {i} clicked");
                    wingItems[i].onClick?.Invoke();
                    return;
                }
            }
        }
        
        // メニュー外をクリックした場合はMovableWindowに処理を任せる
        Debug.Log("[WingMenu] Clicked outside menu, letting MovableWindow handle it");
        hasProcessedClick = false;
    }

    private bool IsAvatarObject(GameObject obj)
    {
        if (vrmLoader == null || vrmLoader.LoadedModel == null) return false;
        
        // オブジェクトがアバターの子要素かチェック
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.gameObject == vrmLoader.LoadedModel)
                return true;
            current = current.parent;
        }
        return false;
    }

    private void HandleHover()
    {
        if (!isMenuOpen) return;
        
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        int newHoveredIndex = -1;
        
        if (Physics.Raycast(ray, out hit, 100f, 1 << menuLayer))
        {
            for (int i = 0; i < wingItems.Count; i++)
            {
                if (hit.collider.gameObject == wingItems[i].wingObject)
                {
                    newHoveredIndex = i;
                    break;
                }
            }
        }
        
        if (newHoveredIndex != hoveredIndex)
        {
            // 前のホバーのEmission効果を無効化
            if (hoveredIndex >= 0 && hoveredIndex < wingItems.Count)
            {
                var prevRenderer = wingItems[hoveredIndex].wingObject.GetComponent<MeshRenderer>();
                if (prevRenderer != null && prevRenderer.material != null)
                {
                    SetHoverEmission(prevRenderer.material, false);
                }
            }
            
            hoveredIndex = newHoveredIndex;
            
            // 新しい色制御システムで色を更新
            UpdateWingColors();
        }
    }

    private void ToggleMenu()
    {
        if (isMenuOpen)
            HideMenu();
        else
            ShowMenu();
    }

    private void ShowMenu()
    {
        if (isAnimating || isMenuOpen) return;
        
        Debug.Log("[WingMenu] ShowMenu called");
        
        isMenuOpen = true;
        menuContainer.SetActive(true);
        
        // 位置とスケールを適切に設定
        AdjustMenuSystem();
        
        Debug.Log($"[WingMenu] Menu positioned at: {menuContainer.transform.position}, scale: {menuContainer.transform.localScale}");
        
        // アニメーション付きで表示
        StartCoroutine(AnimateMenuOpen());
    }

    private void ShowMenuAtCenter()
    {
        if (isAnimating || isMenuOpen) return;
        
        isMenuOpen = true;
        menuContainer.SetActive(true);
        
        // VRM読み込み前は画面中央（0,0,0）に配置、スケール1,1,1
        menuContainer.transform.position = new Vector3(0, 0, 0);
        menuContainer.transform.localScale = Vector3.one;
        
        Debug.Log("[WingMenu] ShowMenuAtCenter - VRM not loaded, position: (0,0,0), scale: (1,1,1)");
        
        StartCoroutine(AnimateMenuOpen());
    }

    private void HideMenu()
    {
        if (isAnimating || !isMenuOpen) return;
        
        // メニューが閉じられたことを記録
        hasEverBeenClosed = true;
        
        StartCoroutine(AnimateMenuClose());
    }

    private void HideMenuImmediate()
    {
        isMenuOpen = false;
        menuContainer.SetActive(false);
        
        foreach (var item in wingItems)
        {
            item.wingObject.transform.localPosition = Vector3.zero;
            item.wingObject.transform.localScale = Vector3.zero;
        }
        
        // パーティクルもクリア
        ClearAllParticles();
    }

    private IEnumerator AnimateMenuOpen()
    {
        isAnimating = true;
        Debug.Log("[WingMenu] AnimateMenuOpen started");
        
        // 各羽を順番にアニメーション
        for (int i = 0; i < wingItems.Count; i++)
        {
            StartCoroutine(AnimateWingOpen(wingItems[i], i * 0.05f));
        }
        
        yield return new WaitForSeconds(animationDuration + 0.05f * wingItems.Count);
        isAnimating = false;
        Debug.Log("[WingMenu] AnimateMenuOpen completed");
    }

    private IEnumerator AnimateWingOpen(WingMenuItem item, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"[WingMenu] AnimateWingOpen started for {item.wingObject.name}");
        
        float elapsed = 0;
        Vector3 startPos = Vector3.zero;
        Vector3 startScale = Vector3.zero;
        Quaternion startRot = Quaternion.identity;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            
            item.wingObject.transform.localPosition = Vector3.Lerp(startPos, item.targetPosition, t);
            item.wingObject.transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
            item.wingObject.transform.localRotation = Quaternion.Lerp(startRot, item.targetRotation, t);
            
            yield return null;
        }
        
        item.wingObject.transform.localPosition = item.targetPosition;
        item.wingObject.transform.localScale = Vector3.one;
        item.wingObject.transform.localRotation = item.targetRotation;
        
        Debug.Log($"[WingMenu] AnimateWingOpen completed for {item.wingObject.name} - " +
                 $"pos: {item.wingObject.transform.localPosition}, " +
                 $"scale: {item.wingObject.transform.localScale}");
    }

    private IEnumerator AnimateMenuClose()
    {
        isAnimating = true;
        
        // 各羽を逆順でアニメーション
        for (int i = wingItems.Count - 1; i >= 0; i--)
        {
            StartCoroutine(AnimateWingClose(wingItems[i], (wingItems.Count - 1 - i) * 0.03f));
        }
        
        yield return new WaitForSeconds(animationDuration + 0.03f * wingItems.Count);
        
        menuContainer.SetActive(false);
        isMenuOpen = false;
        isAnimating = false;
    }

    private IEnumerator AnimateWingClose(WingMenuItem item, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        float elapsed = 0;
        Vector3 startPos = item.wingObject.transform.localPosition;
        Vector3 startScale = item.wingObject.transform.localScale;
        Quaternion startRot = item.wingObject.transform.localRotation;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            
            item.wingObject.transform.localPosition = Vector3.Lerp(startPos, Vector3.zero, t);
            item.wingObject.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            item.wingObject.transform.localRotation = Quaternion.Lerp(startRot, Quaternion.identity, t);
            
            yield return null;
        }
        
        item.wingObject.transform.localPosition = Vector3.zero;
        item.wingObject.transform.localScale = Vector3.zero;
    }

    // メニュー機能
    private void OnExitClick()
    {
        Debug.Log("EXIT clicked - closing application");
        
        // HTTP terminateと同じ確実な終了パスを使用
        var animationServer = AnimationServer.Instance;
        if (animationServer != null) {
            Debug.Log("[WingMenu] Using AnimationServer.InvokeShutdown() for reliable termination with config save");
            animationServer.InvokeShutdown();
        } else {
            Debug.LogWarning("[WingMenu] AnimationServer not found, falling back to direct termination");
            
            // フォールバック: 明示的に設定保存してから終了
            var transparentWindow = FindFirstObjectByType<TransparentWindow>();
            if (transparentWindow != null) {
                transparentWindow.SaveWindowBoundsIfNeeded();
                Debug.Log("[WingMenu] Window bounds saved before fallback termination");
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void OnPlaceholderClick(int index)
    {
        Debug.Log($"Wing menu item {index + 1} clicked (placeholder)");
        // 将来的に機能を追加

        // メニューを閉じる（カーソルにくっつく問題を解決）
        HideMenu();
    }

    // LateUpdate()を削除 - カメラにくっつかないように
    // private void LateUpdate()
    // {
    //     if (isMenuOpen && mainCamera != null)
    //     {
    //         UpdateMenuTransform();
    //     }
    // }

    private void UpdateMenuTransform()
    {
        AdjustMenuSystem();
    }
    
    private void AdjustMenuSystem()
    {
        if (mainCamera == null) {
            Debug.LogError("[WingMenu] Main camera not found for menu adjustment");
            return;
        }

        // VRM読み込み後は適切なスケールと位置に調整
        if (vrmLoader != null && vrmLoader.LoadedModel != null) {
            // VRMのheadBoneの位置を取得（VRMLoader.csのAdjustCamera()と同じ方法）
            Transform headBone = GetHeadBone(vrmLoader.LoadedModel);
            
            if (headBone != null) {
                // headBoneの位置を基準にメニューを配置
                Vector3 headPosition = headBone.position;
                Vector3 modelForward = GetModelForward(vrmLoader.LoadedModel);
                
                // VRoidモデルの場合は向きを反転
                if (IsVroidModel(vrmLoader.LoadedModel)) {
                    modelForward = -vrmLoader.LoadedModel.transform.forward;
                }
                
                // メニューをVRMの後ろ（背景の手前）に配置
                // Z座標を-2に固定
                Vector3 menuPosition = new Vector3(headPosition.x, headPosition.y - 0.3f, -2.0f);
                
                menuContainer.transform.position = menuPosition;
                menuContainer.transform.localScale = Vector3.one * wingScale; // wingScaleを使用
                
                // メニューをカメラの方向に向ける（カメラが180度回転しているので調整）
                Vector3 cameraDirection = (mainCamera.transform.position - menuPosition).normalized;
                // カメラの実際の向きを考慮してメニューを向ける
                Vector3 menuForward = -mainCamera.transform.forward; // カメラが見ている方向の逆
                menuContainer.transform.rotation = Quaternion.LookRotation(menuForward, Vector3.up);
                
                Debug.Log($"[WingMenu] VRM loaded - Menu positioned at: {menuPosition}, scale: {wingScale}, headPos: {headPosition}");
            } else {
                // headBoneが見つからない場合はカメラ基準で配置
                Vector3 menuPosition = mainCamera.transform.position + mainCamera.transform.forward * (mainCamera.nearClipPlane + 0.05f);
                menuContainer.transform.position = menuPosition;
                menuContainer.transform.localScale = Vector3.one * wingScale;
                menuContainer.transform.rotation = Quaternion.identity;
                
                Debug.Log($"[WingMenu] VRM loaded but no headBone - Menu positioned at: {menuPosition}, scale: {wingScale}");
            }
        } else {
            // VRM読み込み前：原点に配置、スケール1,1,1
            menuContainer.transform.position = Vector3.zero;
            menuContainer.transform.localScale = Vector3.one;
            menuContainer.transform.rotation = Quaternion.identity;
            
            Debug.Log("[WingMenu] VRM not loaded - Menu at origin, scale: 1,1,1");
        }
    }
    
    // MovableWindowとの競合回避用
    public bool HasProcessedClick()
    {
        return hasProcessedClick;
    }
    
    // MovableWindowがメニューの状態を確認するため
    public bool IsMenuOpen()
    {
        return isMenuOpen;
    }
    
    // テスト用：アニメーションなしで即座に表示
    private void ShowMenuImmediate()
    {
        Debug.Log("[WingMenu] ShowMenuImmediate - displaying wings without animation");
        
        for (int i = 0; i < wingItems.Count; i++)
        {
            var item = wingItems[i];
            item.wingObject.transform.localPosition = item.targetPosition;
            item.wingObject.transform.localScale = Vector3.one;
            item.wingObject.transform.localRotation = item.targetRotation;
            
            // デバッグ：ワールド座標も確認
            Debug.Log($"[WingMenu] Wing {i} set to target: " +
                     $"localPos={item.wingObject.transform.localPosition}, " +
                     $"worldPos={item.wingObject.transform.position}, " +
                     $"scale={item.wingObject.transform.localScale}, " +
                     $"layer={item.wingObject.layer} ({LayerMask.LayerToName(item.wingObject.layer)})");
            
            // レンダラーの状態も確認
            var renderer = item.wingObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Debug.Log($"[WingMenu] Wing {i} renderer: enabled={renderer.enabled}, " +
                         $"material={renderer.material.name}, " +
                         $"color={renderer.material.color}");
            }
            
            // メッシュの状態も確認
            var meshFilter = item.wingObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                Debug.Log($"[WingMenu] Wing {i} mesh: vertices={meshFilter.mesh.vertexCount}, " +
                         $"bounds={meshFilter.mesh.bounds}");
            }
        }
        
        isAnimating = false;
    }

    private void OnVrmModelLoaded(GameObject vrmModel) {
        Debug.Log("[WingMenu] VRM model loaded - adjusting menu system");
        if (isMenuOpen) {
            AdjustMenuSystem();
            // デバッグ情報を出力
            DebugMenuVisibility();
        }
        
        // VRM読み込み後の色のくすみ対策：全ての羽にEmissionを適用
        ApplyPostVRMEmission();
    }

    // デバッグ用：遅延してデバッグ情報を出力
    private IEnumerator DelayedDebugInfo() {
        yield return new WaitForSeconds(0.5f); // アニメーション完了を待つ
        DebugMenuVisibility();
    }

    // デバッグ用：メニューの可視性をチェック
    private void DebugMenuVisibility() {
        Debug.Log("=== WingMenu Visibility Debug ===");
        
        // カメラ情報
        if (mainCamera != null) {
            Debug.Log($"Camera: {mainCamera.name}");
            Debug.Log($"Camera Position: {mainCamera.transform.position}");
            Debug.Log($"Camera Rotation: {mainCamera.transform.rotation.eulerAngles}");
            Debug.Log($"Camera Culling Mask: {mainCamera.cullingMask} (binary: {System.Convert.ToString(mainCamera.cullingMask, 2)})");
            Debug.Log($"UI Layer (5): {(mainCamera.cullingMask & (1 << 5)) != 0}");
            Debug.Log($"Menu Layer ({menuLayer}): {(mainCamera.cullingMask & (1 << menuLayer)) != 0}");
        }
        
        // メニューコンテナ情報
        if (menuContainer != null) {
            Debug.Log($"MenuContainer Position: {menuContainer.transform.position}");
            Debug.Log($"MenuContainer Scale: {menuContainer.transform.localScale}");
            Debug.Log($"MenuContainer Active: {menuContainer.activeInHierarchy}");
        }
        
        // 各羽の情報
        for (int i = 0; i < wingItems.Count; i++) {
            var item = wingItems[i];
            if (item.wingObject != null) {
                var renderer = item.wingObject.GetComponent<MeshRenderer>();
                Debug.Log($"Wing {i}: Active={item.wingObject.activeInHierarchy}, " +
                         $"Layer={item.wingObject.layer}, " +
                         $"WorldPos={item.wingObject.transform.position}, " +
                         $"LocalScale={item.wingObject.transform.localScale}, " +
                         $"RendererEnabled={renderer?.enabled}, " +
                         $"Material={renderer?.material?.name}, " +
                         $"RenderQueue={renderer?.material?.renderQueue}");
                
                // カメラからの距離
                if (mainCamera != null) {
                    float distance = Vector3.Distance(mainCamera.transform.position, item.wingObject.transform.position);
                    Debug.Log($"Wing {i} distance from camera: {distance}");
                }
            }
        }
        
        Debug.Log("=== End Debug ===");
    }

    // デバッグ用：大きなテストメニューを表示
    private void ShowLargeTestMenu() {
        if (isAnimating) return;
        
        Debug.Log("[WingMenu] ShowLargeTestMenu - creating large visible menu for testing");
        
        isMenuOpen = true;
        menuContainer.SetActive(true);
        
        // カメラの正面に大きく表示
        Vector3 testPosition = mainCamera.transform.position + mainCamera.transform.forward * 2.0f;
        menuContainer.transform.position = testPosition;
        menuContainer.transform.localScale = Vector3.one * 2.0f; // 大きく表示
        menuContainer.transform.rotation = Quaternion.LookRotation(-mainCamera.transform.forward, Vector3.up);
        
        // 各羽を明るい色で即座に表示
        for (int i = 0; i < wingItems.Count; i++) {
            var item = wingItems[i];
            item.wingObject.transform.localPosition = item.targetPosition;
            item.wingObject.transform.localScale = Vector3.one;
            item.wingObject.transform.localRotation = item.targetRotation;
            
            // 明るい色に変更
            var renderer = item.wingObject.GetComponent<MeshRenderer>();
            if (renderer != null) {
                renderer.material.color = new Color(1f, 0f, 1f, 1f); // マゼンタ色で目立つように
            }
            
            Debug.Log($"[WingMenu] Test Wing {i}: WorldPos={item.wingObject.transform.position}, " +
                     $"LocalPos={item.wingObject.transform.localPosition}, " +
                     $"Scale={item.wingObject.transform.localScale}");
        }
        
        Debug.Log($"[WingMenu] Large test menu positioned at: {testPosition}, scale: 2.0");
        
        // デバッグ情報も出力
        StartCoroutine(DelayedDebugInfo());
    }

    // 虹色エフェクト関連メソッド
    private Material CreateRainbowMaterial(string wingName)
    {
        Material material = null;
        
        if (enableRainbowEffect)
        {
            // 透明対応のStandardシェーダーを使用
            Shader shader = Shader.Find("Standard");
            if (shader != null)
            {
                material = new Material(shader);
                
                // 透明モードに設定
                material.SetFloat("_Mode", 3); // Transparent
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000; // Transparent queue
                
                // カリングを無効にして両面表示
                if (material.HasProperty("_Cull"))
                {
                    material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                }
                
                // 初期色を設定（後でUpdateRainbowColorsで更新される）
                Color initialColor = GetRainbowColor(0, 0);
                material.color = initialColor;
                
                Debug.Log($"[WingMenu] Created rainbow material for {wingName} with transparent Standard shader");
            }
            else
            {
                Debug.LogWarning("[WingMenu] Standard shader not found, falling back to basic material");
                material = CreateBasicMaterial();
            }
        }
        else
        {
            // 虹色エフェクトが無効の場合は従来のマテリアル
            material = CreateBasicMaterial();
        }
        
        return material;
    }
    
    private Material CreateBasicMaterial()
    {
        Material material = null;
        
        // 1. まずUnlit/Colorを試す（最も確実）
        Shader shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            material = new Material(shader);
            material.color = wingColor;
            // カリングを無効にして両面表示
            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            Debug.Log($"[WingMenu] Using Unlit/Color shader with color: {wingColor}");
        }
        else
        {
            // 2. Standardを試す
            shader = Shader.Find("Standard");
            if (shader != null)
            {
                material = new Material(shader);
                material.color = wingColor;
                // Opaqueモードに設定
                material.SetFloat("_Mode", 0); // Opaque
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
                // カリングを無効にして両面表示
                if (material.HasProperty("_Cull"))
                {
                    material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                }
                Debug.Log($"[WingMenu] Using Standard shader with color: {wingColor}");
            }
            else
            {
                // 3. 最後の手段でSprites/Default
                shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    material = new Material(shader);
                    material.color = wingColor;
                    Debug.LogWarning("[WingMenu] Using Sprites/Default shader as fallback");
                }
                else
                {
                    Debug.LogError("[WingMenu] No suitable shader found!");
                    return null;
                }
            }
        }
        
        return material;
    }
    
    private void UpdateRainbowColors()
    {
        // 新しい色制御システムを使用
        UpdateWingColors();
    }
    
    private Color GetRainbowColor(int wingIndex, float time)
    {
        // すべての羽が同じ色で統一（ゲーミング効果）
        // 時間ベースで高速に色相を変化させる（定数で速度調整）
        float hue = (time * rainbowSpeed * RAINBOW_SPEED_MULTIPLIER) % 1f;
        
        // HSVからRGBに変換（白ベースの虹色）
        Color rainbowColor = Color.HSVToRGB(hue, saturation, brightnessMultiplier);
        
        // 透明度を設定
        rainbowColor.a = wingTransparency;
        
        return rainbowColor;
    }

    // パーティクルシステム関連メソッド
    private void UpdateSparkleParticles()
    {
        // 既存のパーティクルを更新
        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            var particle = activeParticles[i];
            
            // ライフタイムを減らす
            particle.lifetime -= Time.deltaTime;
            
            if (particle.lifetime <= 0f)
            {
                // パーティクルを削除
                if (particle.particleObject != null)
                {
                    DestroyImmediate(particle.particleObject);
                }
                activeParticles.RemoveAt(i);
                continue;
            }
            
            // パーティクルの位置を更新
            if (particle.particleObject != null)
            {
                particle.particleObject.transform.localPosition += particle.velocity * Time.deltaTime;
                
                // フェードアウト効果
                float alpha = Mathf.Lerp(0f, particle.startAlpha, particle.lifetime / particle.maxLifetime);
                var renderer = particle.particleObject.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    Color currentColor = renderer.material.color;
                    
                    // 虹色エフェクトが有効な場合は虹色を適用
                    if (enableRainbowEffect)
                    {
                        currentColor = GetRainbowColor(0, Time.time);
                    }
                    else
                    {
                        currentColor = Color.white;
                    }
                    
                    currentColor.a = alpha;
                    renderer.material.color = currentColor;
                }
            }
        }
    }
    
    private void SpawnSparkleParticles()
    {
        // スポーン間隔チェック
        if (Time.time - lastParticleSpawnTime < particleSpawnInterval)
            return;
        
        lastParticleSpawnTime = Time.time;
        
        // 各羽の周りにパーティクルを生成
        for (int wingIndex = 0; wingIndex < wingItems.Count; wingIndex++)
        {
            if (wingItems[wingIndex].wingObject == null) continue;
            
            // この羽の現在のパーティクル数をチェック
            int currentParticleCount = 0;
            foreach (var particle in activeParticles)
            {
                if (particle.wingIndex == wingIndex)
                    currentParticleCount++;
            }
            
            // 最大数に達していない場合のみ生成
            if (currentParticleCount < particlesPerWing)
            {
                CreateSparkleParticle(wingIndex);
            }
        }
    }
    
    private void CreateSparkleParticle(int wingIndex)
    {
        var wingObject = wingItems[wingIndex].wingObject;
        
        // パーティクルオブジェクト作成
        GameObject particleObj = new GameObject($"SparkleParticle_Wing{wingIndex}");
        particleObj.layer = menuLayer;
        particleObj.transform.SetParent(wingObject.transform);
        
        // 羽の周辺にランダム配置
        Vector3 randomOffset = new Vector3(
            Random.Range(-0.3f, 0.3f),
            Random.Range(-0.4f, 0.4f),
            Random.Range(-0.1f, 0.1f)
        );
        particleObj.transform.localPosition = randomOffset;
        
        // 小さなキューブメッシュを作成
        MeshFilter meshFilter = particleObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = particleObj.AddComponent<MeshRenderer>();
        
        // シンプルなキューブメッシュ
        meshFilter.mesh = CreateSparkleParticleMesh();
        
        // パーティクル用マテリアル作成
        Material particleMaterial = CreateSparkleParticleMaterial();
        meshRenderer.material = particleMaterial;
        
        // レンダリング順序を羽より前に
        meshRenderer.sortingOrder = 110;
        
        // パーティクルのスケール設定
        particleObj.transform.localScale = Vector3.one * particleSize;
        
        // パーティクルデータ作成
        SparkleParticle particle = new SparkleParticle
        {
            particleObject = particleObj,
            lifetime = particleLifetime,
            maxLifetime = particleLifetime,
            velocity = new Vector3(
                Random.Range(-0.1f, 0.1f),
                Random.Range(0.1f, 0.3f),  // 上向きに移動
                Random.Range(-0.05f, 0.05f)
            ),
            startAlpha = 0.8f,
            wingIndex = wingIndex
        };
        
        activeParticles.Add(particle);
    }
    
    private Mesh CreateSparkleParticleMesh()
    {
        Mesh mesh = new Mesh();
        
        // シンプルな5つ星の形状を作成（軽量版）
        float outerRadius = 0.5f;
        float innerRadius = 0.2f;
        int starPoints = 5;
        
        // 固定サイズの配列を使用してメモリ効率を改善
        Vector3[] vertices = new Vector3[11]; // 中心1 + 星の頂点10
        int[] triangles = new int[30]; // 10個の三角形 * 3頂点
        
        // 中心点
        vertices[0] = Vector3.zero;
        
        // 星の頂点を計算
        for (int i = 0; i < starPoints * 2; i++)
        {
            float angle = i * Mathf.PI / starPoints;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
        }
        
        // 三角形を作成（中心から各辺へ）
        for (int i = 0; i < starPoints * 2; i++)
        {
            int triIndex = i * 3;
            triangles[triIndex] = 0; // 中心点
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = ((i + 1) % (starPoints * 2)) + 1;
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        return mesh;
    }
    
    private Material CreateSparkleParticleMaterial()
    {
        // 透明対応のStandardシェーダーを使用
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        
        Material material = new Material(shader);
        
        if (shader.name == "Standard")
        {
            // 透明モードに設定
            material.SetFloat("_Mode", 3); // Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000; // Transparent queue
        }
        
        // 初期色を白に設定
        material.color = new Color(1f, 1f, 1f, 0.8f);
        
        return material;
    }
    
    private void ClearAllParticles()
    {
        // すべてのパーティクルを削除
        foreach (var particle in activeParticles)
        {
            if (particle.particleObject != null)
            {
                DestroyImmediate(particle.particleObject);
            }
        }
        activeParticles.Clear();
    }

    // ホバー時のEmission効果
    private void SetHoverEmission(Material material, bool enable)
    {
        if (material == null) return;
        
        if (enable)
        {
            // Emissionを有効にして白く光らせる
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.white * 0.5f); // 適度な明るさ
            
            // 通常の色も白に設定
            Color whiteColor = Color.white;
            if (enableRainbowEffect)
            {
                whiteColor.a = wingTransparency;
            }
            material.color = whiteColor;
        }
        else
        {
            // VRM読み込み後は基本のEmissionを維持（完全に無効化しない）
            if (vrmLoader != null && vrmLoader.LoadedModel != null)
            {
                // VRM読み込み後は基本のEmission（RGB: 180,180,180）を維持
                material.EnableKeyword("_EMISSION");
                Color baseEmissionColor = new Color(0.706f, 0.706f, 0.706f, 1.0f);
                material.SetColor("_EmissionColor", baseEmissionColor);
            }
            else
            {
                // VRM読み込み前は従来通りEmissionを無効にする
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    // VRMLoader.csと同じヘルパーメソッドを追加
    private Transform GetHeadBone(GameObject model) {
        Animator animator = model.GetComponent<Animator>();
        if (animator != null && animator.isHuman) {
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null) return headBone;
        }
        return FindNodeByName(model.transform, new string[] { "Head", "J_Bip_C_Head" });
    }

    private Vector3 GetModelForward(GameObject model) {
        Animator animator = model.GetComponent<Animator>();
        if (animator != null && animator.isHuman) {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) return hips.forward;
        }

        // 名前マッチでヒップノードを取得（念のためフォールバック）
        Transform hipsNode = FindNodeByName(model.transform, new string[] { "Hips", "Root", "J_Bip_C_Hips" });
        return hipsNode != null ? hipsNode.forward : Vector3.forward;
    }

    private bool IsVroidModel(GameObject model) {
        Transform hipsNode = FindNodeByName(model.transform, new string[] { "Root", "J_Bip_C_Hips" });
        Transform headNode = FindNodeByName(model.transform, new string[] { "Head", "J_Bip_C_Head" });
        return (hipsNode != null && headNode != null);
    }

    private Transform FindNodeByName(Transform root, string[] keywords) {
        foreach (Transform child in root) {
            string name = child.name.ToLowerInvariant();
            foreach (string kw in keywords) {
                if (name.Contains(kw.ToLowerInvariant())) {
                    return child;
                }
            }
            // 再帰検索
            var hit = FindNodeByName(child, keywords);
            if (hit != null) return hit;
        }
        return null;
    }

    #region HTTP制御用メソッド

    /// <summary>
    /// HTTP経由でメニューを表示する
    /// </summary>
    /// <param name="side">表示する側 ("left", "right", null=両方)</param>
    public void ShowMenuViaHttp(string side = null)
    {
        Debug.Log($"[WingMenu] ShowMenuViaHttp called with side: {side}");
        
        if (string.IsNullOrEmpty(side))
        {
            // 両方表示
            leftWingsVisible = true;
            rightWingsVisible = true;
            ShowMenu();
        }
        else if (side.ToLower() == "left")
        {
            leftWingsVisible = true;
            UpdateWingVisibility();
            if (!isMenuOpen) ShowMenu();
        }
        else if (side.ToLower() == "right")
        {
            rightWingsVisible = true;
            UpdateWingVisibility();
            if (!isMenuOpen) ShowMenu();
        }
    }

    /// <summary>
    /// HTTP経由でメニューを非表示にする
    /// </summary>
    /// <param name="side">非表示にする側 ("left", "right", null=両方)</param>
    public void HideMenuViaHttp(string side = null)
    {
        Debug.Log($"[WingMenu] HideMenuViaHttp called with side: {side}");
        
        if (string.IsNullOrEmpty(side))
        {
            // 両方非表示
            leftWingsVisible = false;
            rightWingsVisible = false;
            HideMenu();
        }
        else if (side.ToLower() == "left")
        {
            leftWingsVisible = false;
            UpdateWingVisibility();
            if (!leftWingsVisible && !rightWingsVisible) HideMenu();
        }
        else if (side.ToLower() == "right")
        {
            rightWingsVisible = false;
            UpdateWingVisibility();
            if (!leftWingsVisible && !rightWingsVisible) HideMenu();
        }
    }

    /// <summary>
    /// HTTP経由でメニューを定義する
    /// </summary>
    /// <param name="allMenus">全8個のメニュー定義（カンマ区切り）</param>
    /// <param name="leftMenus">左4個のメニュー定義（カンマ区切り）</param>
    /// <param name="rightMenus">右4個のメニュー定義（カンマ区切り）</param>
    public void DefineMenusViaHttp(string allMenus, string leftMenus, string rightMenus)
    {
        Debug.Log($"[WingMenu] DefineMenusViaHttp called - all: {allMenus}, left: {leftMenus}, right: {rightMenus}");
        
        if (!string.IsNullOrEmpty(allMenus))
        {
            // 全メニュー定義
            string[] menuArray = allMenus.Split(',');
            for (int i = 0; i < 8 && i < menuArray.Length; i++)
            {
                string menuLabel = menuArray[i].Trim();
                if (string.IsNullOrEmpty(menuLabel))
                {
                    menuLabel = "placeholder";
                }
                menuLabels[i] = menuLabel;
                UpdateWingAction(i, menuLabel);
            }
        }
        else
        {
            // 左右個別定義
            if (!string.IsNullOrEmpty(leftMenus))
            {
                string[] leftArray = leftMenus.Split(',');
                for (int i = 0; i < 4 && i < leftArray.Length; i++)
                {
                    string menuLabel = leftArray[i].Trim();
                    if (string.IsNullOrEmpty(menuLabel))
                    {
                        menuLabel = "placeholder";
                    }
                    menuLabels[i] = menuLabel;
                    UpdateWingAction(i, menuLabel);
                }
            }
            
            if (!string.IsNullOrEmpty(rightMenus))
            {
                string[] rightArray = rightMenus.Split(',');
                for (int i = 0; i < 4 && i < rightArray.Length; i++)
                {
                    string menuLabel = rightArray[i].Trim();
                    if (string.IsNullOrEmpty(menuLabel))
                    {
                        menuLabel = "placeholder";
                    }
                    menuLabels[i + 4] = menuLabel;
                    UpdateWingAction(i + 4, menuLabel);
                }
            }
        }
    }

    /// <summary>
    /// HTTP経由でメニューをクリアする（デフォルト状態：5番目にexitのみ）
    /// </summary>
    public void ClearMenusViaHttp()
    {
        Debug.Log("[WingMenu] ClearMenusViaHttp called");
        
        // 全てプレースホルダーに設定
        for (int i = 0; i < 8; i++)
        {
            if (i == 4) // 5番目（インデックス4）はexit
            {
                menuLabels[i] = "exit";
                UpdateWingAction(i, "exit");
            }
            else
            {
                menuLabels[i] = "placeholder";
                UpdateWingAction(i, "placeholder");
            }
        }
    }

    /// <summary>
    /// HTTP経由で羽の設定を変更する
    /// </summary>
    public void ConfigureWingsViaHttp(int? leftLength, int? rightLength, float? angleDelta, float? angleStart)
    {
        Debug.Log($"[WingMenu] ConfigureWingsViaHttp called - leftLength: {leftLength}, rightLength: {rightLength}, angleDelta: {angleDelta}, angleStart: {angleStart}");
        
        bool needsRecreate = false;
        
        if (leftLength.HasValue && leftLength.Value != leftWingCount)
        {
            leftWingCount = leftLength.Value;
            needsRecreate = true;
        }
        
        if (rightLength.HasValue && rightLength.Value != rightWingCount)
        {
            rightWingCount = rightLength.Value;
            needsRecreate = true;
        }
        
        if (angleDelta.HasValue)
        {
            this.angleDelta = angleDelta.Value;
            needsRecreate = true;
        }
        
        if (angleStart.HasValue)
        {
            this.angleStart = angleStart.Value;
            needsRecreate = true;
        }
        
        if (needsRecreate)
        {
            RecreateWingItems();
        }
    }

    /// <summary>
    /// HTTP経由でメニューの変形を設定する
    /// </summary>
    public void SetTransformViaHttp(Vector3? position, Vector3? rotation, Vector3? scale)
    {
        Debug.Log($"[WingMenu] SetTransformViaHttp called - position: {position}, rotation: {rotation}, scale: {scale}");
        
        if (menuContainer == null) return;
        
        if (position.HasValue)
        {
            menuContainer.transform.position = position.Value;
        }
        
        if (rotation.HasValue)
        {
            menuContainer.transform.rotation = Quaternion.Euler(rotation.Value);
        }
        
        if (scale.HasValue)
        {
            menuContainer.transform.localScale = scale.Value;
        }
    }

    /// <summary>
    /// HTTP経由でメニューの状態を取得する
    /// </summary>
    public Dictionary<string, object> GetMenuStatusViaHttp()
    {
        var status = new Dictionary<string, object>();
        
        // 可視性情報
        var visible = new Dictionary<string, object>
        {
            {"left", leftWingsVisible},
            {"right", rightWingsVisible}
        };
        status["visible"] = visible;
        
        // 位置・回転・スケール情報
        if (menuContainer != null)
        {
            var pos = menuContainer.transform.position;
            var rot = menuContainer.transform.rotation.eulerAngles;
            var scl = menuContainer.transform.localScale;
            
            status["position"] = new Dictionary<string, object> {{"x", pos.x}, {"y", pos.y}, {"z", pos.z}};
            status["rotation"] = new Dictionary<string, object> {{"x", rot.x}, {"y", rot.y}, {"z", rot.z}};
            status["scale"] = new Dictionary<string, object> {{"x", scl.x}, {"y", scl.y}, {"z", scl.z}};
        }
        
        // 設定情報
        var config = new Dictionary<string, object>
        {
            {"left_length", leftWingCount},
            {"right_length", rightWingCount},
            {"angle_delta", angleDelta},
            {"angle_start", angleStart}
        };
        status["config"] = config;
        
        // メニュー情報
        var menus = new Dictionary<string, object>();
        var leftMenus = new List<Dictionary<string, object>>();
        var rightMenus = new List<Dictionary<string, object>>();
        
        for (int i = 0; i < 8; i++)
        {
            string label = i < menuLabels.Length ? menuLabels[i] : "placeholder";
            if (string.IsNullOrEmpty(label)) label = "placeholder";
            
            string type = GetMenuType(label);
            var menuInfo = new Dictionary<string, object>
            {
                {"index", i},
                {"label", label},
                {"type", type}
            };
            
            if (i < 4)
            {
                leftMenus.Add(menuInfo);
            }
            else
            {
                rightMenus.Add(menuInfo);
            }
        }
        
        menus["left"] = leftMenus;
        menus["right"] = rightMenus;
        status["menus"] = menus;
        
        return status;
    }

    /// <summary>
    /// 羽の可視性を更新する
    /// </summary>
    private void UpdateWingVisibility()
    {
        if (wingItems == null) return;
        
        for (int i = 0; i < wingItems.Count; i++)
        {
            if (wingItems[i].wingObject == null) continue;
            
            bool shouldBeVisible = false;
            if (i < 4) // 左側
            {
                shouldBeVisible = leftWingsVisible;
            }
            else // 右側
            {
                shouldBeVisible = rightWingsVisible;
            }
            
            wingItems[i].wingObject.SetActive(shouldBeVisible && isMenuOpen);
        }
    }

    /// <summary>
    /// 羽のアクションを更新する
    /// </summary>
    private void UpdateWingAction(int index, string label)
    {
        if (index < 0 || index >= wingItems.Count) return;
        
        wingItems[index].label = label;
        
        if (WingMenuCommandHandler.IsBuiltinFunction(label))
        {
            wingItems[index].onClick = () => ExecuteBuiltinFunction(label);
        }
        else
        {
            wingItems[index].onClick = () => OnCustomMenuClick(index, label);
        }
    }

    /// <summary>
    /// Built-in Functionを実行する
    /// </summary>
    private void ExecuteBuiltinFunction(string functionName)
    {
        Debug.Log($"[WingMenu] Executing builtin function: {functionName}");
        
        switch (functionName.ToLower())
        {
            case "reset_pose":
                ExecuteResetPose();
                break;
                
            case "reset_shape":
                ExecuteResetShape();
                break;
                
            case "exit":
                OnExitClick();
                break;
                
            default:
                Debug.LogWarning($"[WingMenu] Unknown builtin function: {functionName}");
                break;
        }
    }

    /// <summary>
    /// ポーズリセット（AGIA待機アニメーション）
    /// </summary>
    private void ExecuteResetPose()
    {
        var animationHandler = FindObjectOfType<AnimationHandler>();
        if (animationHandler != null && animationHandler.IsInitialized)
        {
            animationHandler.ResetAGIAAnimation();
            Debug.Log("[WingMenu] Reset pose executed");
        }
        else
        {
            Debug.LogWarning("[WingMenu] AnimationHandler not found or not initialized");
        }
    }

    /// <summary>
    /// 口の形状リセット（全表情ウェイトを0に）
    /// </summary>
    private void ExecuteResetShape()
    {
        var vrmLoader = FindObjectOfType<VRMLoader>();
        if (vrmLoader?.VrmInstance?.Runtime?.Expression != null)
        {
            var expression = vrmLoader.VrmInstance.Runtime.Expression;
            foreach (var exKey in expression.ExpressionKeys)
            {
                expression.SetWeight(exKey, 0.0f);
            }
            Debug.Log("[WingMenu] Reset shape executed");
        }
        else
        {
            Debug.LogWarning("[WingMenu] VRM Expression system not available");
        }
    }

    /// <summary>
    /// カスタムメニューのクリック処理（将来のIoT拡張用）
    /// </summary>
    private void OnCustomMenuClick(int index, string label)
    {
        Debug.Log($"[WingMenu] Custom menu clicked - index: {index}, label: {label}");
        
        // 現在はプレースホルダー処理
        if (label == "placeholder")
        {
            OnPlaceholderClick(index);
        }
        else
        {
            // 将来的にはここでHTTP APIコールを実装
            Debug.Log($"[WingMenu] Future IoT action: {label}");
            HideMenu(); // とりあえずメニューを閉じる
        }
    }

    /// <summary>
    /// メニューラベルのタイプを取得する
    /// </summary>
    private string GetMenuType(string label)
    {
        if (WingMenuCommandHandler.IsBuiltinFunction(label))
        {
            return "builtin";
        }
        else if (label == "placeholder")
        {
            return "placeholder";
        }
        else
        {
            return "future_iot";
        }
    }

    /// <summary>
    /// 羽アイテムを再作成する（設定変更時）
    /// </summary>
    private void RecreateWingItems()
    {
        Debug.Log("[WingMenu] Recreating wing items with new configuration");
        
        // 既存の羽を削除
        foreach (var item in wingItems)
        {
            if (item.wingObject != null)
            {
                DestroyImmediate(item.wingObject);
            }
        }
        wingItems.Clear();
        
        // 新しい設定で羽を再作成
        CreateWingItemsWithConfig();
        
        // メニューが開いている場合は再表示
        if (isMenuOpen)
        {
            AdjustMenuSystem();
            UpdateWingVisibility();
        }
        
        // VRM読み込み後の場合は、Emissionを再適用
        if (vrmLoader != null && vrmLoader.LoadedModel != null)
        {
            Debug.Log("[WingMenu] Reapplying emission after wing items recreation");
            ApplyPostVRMEmission();
        }
    }

    /// <summary>
    /// 設定を考慮して羽アイテムを作成する
    /// </summary>
    private void CreateWingItemsWithConfig()
    {
        // 左側の羽を作成
        for (int i = 0; i < leftWingCount; i++)
        {
            var wingItem = new WingMenuItem();
            
            wingItem.wingObject = CreateWingMesh($"Wing_Left_{i + 1}");
            wingItem.wingObject.transform.SetParent(menuContainer.transform);
            
            float angle = GetWingAngleWithConfig(i, true);
            float radius = menuRadius;
            
            wingItem.targetPosition = new Vector3(
                -Mathf.Cos(angle) * radius - 0.5f,
                -Mathf.Sin(angle) * radius * 0.8f,
                0.0f
            );
            wingItem.targetRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 45);
            
            // ラベルとアクションを設定
            string label = i < menuLabels.Length ? menuLabels[i] : "placeholder";
            if (string.IsNullOrEmpty(label)) label = "placeholder";
            
            wingItem.label = label;
            UpdateWingAction(wingItems.Count, label);
            
            wingItems.Add(wingItem);
        }
        
        // 右側の羽を作成
        for (int i = 0; i < rightWingCount; i++)
        {
            var wingItem = new WingMenuItem();
            
            wingItem.wingObject = CreateWingMesh($"Wing_Right_{i + 1}");
            wingItem.wingObject.transform.SetParent(menuContainer.transform);
            
            float angle = GetWingAngleWithConfig(i, false);
            float radius = menuRadius;
            
            wingItem.targetPosition = new Vector3(
                Mathf.Cos(angle) * radius + 0.5f,
                -Mathf.Sin(angle) * radius * 0.8f,
                0.0f
            );
            wingItem.targetRotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg + 45);
            
            // ラベルとアクションを設定
            int labelIndex = leftWingCount + i;
            string label = labelIndex < menuLabels.Length ? menuLabels[labelIndex] : "placeholder";
            if (string.IsNullOrEmpty(label)) label = "placeholder";
            
            wingItem.label = label;
            UpdateWingAction(wingItems.Count, label);
            
            wingItems.Add(wingItem);
        }
    }

    /// <summary>
    /// 設定を考慮した羽の角度を計算する
    /// </summary>
    private float GetWingAngleWithConfig(int index, bool isLeft)
    {
        if (bladeSplitMode == BladeSplitMode.Keep)
        {
            // blade_split=keep：0-360度の連続配置（一筆書き）
            int totalWings = leftWingCount + rightWingCount;
            if (totalWings <= 1) return angleStart * Mathf.Deg2Rad;
            
            // 全体のインデックスを計算
            int globalIndex = isLeft ? index : leftWingCount + index;
            
            float startAngle = angleStart;
            float endAngle = angleStart + (angleDelta * (totalWings - 1));
            
            return Mathf.Lerp(startAngle, endAngle, globalIndex / (float)(totalWings - 1)) * Mathf.Deg2Rad;
        }
        else
        {
            // blade_split=reset/split：従来通りの角度計算（左右独立、二筆書き）
            int wingCount = isLeft ? leftWingCount : rightWingCount;
            if (wingCount <= 1) return angleStart * Mathf.Deg2Rad;
            
            float startAngle = angleStart;
            float endAngle = angleStart + (angleDelta * (wingCount - 1));
            
            return Mathf.Lerp(startAngle, endAngle, index / (float)(wingCount - 1)) * Mathf.Deg2Rad;
        }
    }

    /// <summary>
    /// HTTP経由で羽の形状を設定する（共通設定）
    /// </summary>
    public void ConfigureShapeViaHttp(float? length, float? edge, float? modifier, bool bladeSplit = true)
    {
        Debug.Log($"[WingMenu] ConfigureShapeViaHttp called - length: {length}, edge: {edge}, modifier: {modifier}, bladeSplit: {bladeSplit}");
        
        bool needsRecreate = false;
        
        if (length.HasValue && length.Value != bladeLength)
        {
            bladeLength = length.Value;
            // 共通設定の場合は左右も同期
            bladeLeftLength = length.Value;
            bladeRightLength = length.Value;
            needsRecreate = true;
        }
        
        if (edge.HasValue && edge.Value != bladeEdge)
        {
            bladeEdge = edge.Value;
            // 共通設定の場合は左右も同期
            bladeLeftEdge = edge.Value;
            bladeRightEdge = edge.Value;
            needsRecreate = true;
        }
        
        if (modifier.HasValue && modifier.Value != bladeModifier)
        {
            bladeModifier = modifier.Value;
            // 共通設定の場合は左右も同期
            bladeLeftModifier = modifier.Value;
            bladeRightModifier = modifier.Value;
            needsRecreate = true;
        }
        
        // blade_splitパラメータに基づいて動作モードを決定
        // デフォルト（true）: 左右独立設定（互換性のため）
        // false: 連続設定（左右を通してシームレス）
        useIndependentShapes = bladeSplit;
        
        // bladeSplitModeの変更もチェック
        // WingMenuCommandHandler側でsplitモードの判定が追加されたが、
        // ここではbladeSplitパラメータのみで判定（互換性のため）
        BladeSplitMode newMode = bladeSplit ? BladeSplitMode.Reset : BladeSplitMode.Keep;
        if (bladeSplitMode != newMode)
        {
            bladeSplitMode = newMode;
            needsRecreate = true;
        }
        
        if (needsRecreate)
        {
            RecreateWingMeshes();
        }
    }

    /// <summary>
    /// HTTP経由で羽の形状を設定する（左右独立設定）
    /// </summary>
    public void ConfigureShapeIndependentViaHttp(
        float? leftLength, float? leftEdge, float? leftModifier,
        float? rightLength, float? rightEdge, float? rightModifier, bool bladeSplit = true)
    {
        Debug.Log($"[WingMenu] ConfigureShapeIndependentViaHttp called - " +
                 $"left: length={leftLength}, edge={leftEdge}, modifier={leftModifier}, " +
                 $"right: length={rightLength}, edge={rightEdge}, modifier={rightModifier}, " +
                 $"bladeSplit: {bladeSplit}");
        
        bool needsRecreate = false;
        
        // 左側パラメータ
        if (leftLength.HasValue && leftLength.Value != bladeLeftLength)
        {
            bladeLeftLength = leftLength.Value;
            needsRecreate = true;
        }
        
        if (leftEdge.HasValue && leftEdge.Value != bladeLeftEdge)
        {
            bladeLeftEdge = leftEdge.Value;
            needsRecreate = true;
        }
        
        if (leftModifier.HasValue && leftModifier.Value != bladeLeftModifier)
        {
            bladeLeftModifier = leftModifier.Value;
            needsRecreate = true;
        }
        
        // 右側パラメータ
        if (rightLength.HasValue && rightLength.Value != bladeRightLength)
        {
            bladeRightLength = rightLength.Value;
            needsRecreate = true;
        }
        
        if (rightEdge.HasValue && rightEdge.Value != bladeRightEdge)
        {
            bladeRightEdge = rightEdge.Value;
            needsRecreate = true;
        }
        
        if (rightModifier.HasValue && rightModifier.Value != bladeRightModifier)
        {
            bladeRightModifier = rightModifier.Value;
            needsRecreate = true;
        }
        
        // 独立設定モードに切り替え
        useIndependentShapes = true;
        
        // blade_splitパラメータを設定（左右独立設定でもmodifierの計算方法は指定可能）
        BladeSplitMode newMode = bladeSplit ? BladeSplitMode.Reset : BladeSplitMode.Keep;
        if (bladeSplitMode != newMode)
        {
            bladeSplitMode = newMode;
            needsRecreate = true;
        }
        
        if (needsRecreate)
        {
            RecreateWingMeshes();
        }
    }

    /// <summary>
    /// 羽のメッシュを再作成する（形状変更時）
    /// </summary>
    private void RecreateWingMeshes()
    {
        Debug.Log("[WingMenu] Recreating wing meshes with new shape configuration");
        
        // 既存の羽のメッシュを更新
        for (int i = 0; i < wingItems.Count; i++)
        {
            if (wingItems[i].wingObject != null)
            {
                var meshFilter = wingItems[i].wingObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    // 新しい形状でメッシュを再生成
                    meshFilter.mesh = CreateWingMeshWithShape(i);
                }
            }
        }
        
        // VRM読み込み後の場合は、Emissionを再適用
        if (vrmLoader != null && vrmLoader.LoadedModel != null)
        {
            Debug.Log("[WingMenu] Reapplying emission after mesh recreation");
            ApplyPostVRMEmission();
        }
    }

    /// <summary>
    /// 形状パラメータを考慮した羽のメッシュを作成する
    /// </summary>
    private Mesh CreateWingMeshWithShape(int wingIndex)
    {
        Mesh mesh = new Mesh();
        
        // 左右判定
        bool isLeft = wingIndex < leftWingCount;
        int localIndex = isLeft ? wingIndex : wingIndex - leftWingCount;
        
        // 使用するパラメータを決定
        float currentLength, currentEdge, currentModifier;
        int effectiveIndex; // サイズ計算に使用するインデックス
        
        if (useIndependentShapes)
        {
            // 左右独立設定：パラメータは個別だが、modifierの計算方法はbladeSplitModeで決まる
            if (isLeft)
            {
                currentLength = bladeLeftLength;
                currentEdge = bladeLeftEdge;
                currentModifier = bladeLeftModifier;
            }
            else
            {
                currentLength = bladeRightLength;
                currentEdge = bladeRightEdge;
                currentModifier = bladeRightModifier;
            }
        }
        else
        {
            // 共通設定：すべて共通パラメータを使用
            currentLength = bladeLength;
            currentEdge = bladeEdge;
            currentModifier = bladeModifier;
        }
        
        // modifierの計算方法はbladeSplitModeで決まる（左右独立設定でも共通設定でも同じ）
        switch (bladeSplitMode)
        {
            case BladeSplitMode.Reset:
                // blade_split=reset：左右でmodifierをリセット（従来のtrue相当）
                effectiveIndex = localIndex;
                break;
                
            case BladeSplitMode.Split:
                // blade_split=split：左右独立配置で、左右を通して連続的にmodifier適用（二筆書き）
                if (isLeft)
                {
                    effectiveIndex = localIndex;
                }
                else
                {
                    // 右翼：左翼の続きとして連続的に計算
                    effectiveIndex = leftWingCount + localIndex;
                }
                break;
                
            case BladeSplitMode.Keep:
                // blade_split=keep：0-360度配置で、左右を通して連続的にmodifier適用（一筆書き）
                if (isLeft)
                {
                    effectiveIndex = localIndex;
                }
                else
                {
                    // 右翼：Y座標の並び順に合わせて逆順にする（上から下へ小さくなる）
                    int totalWings = leftWingCount + rightWingCount;
                    effectiveIndex = totalWings - 1 - localIndex;
                }
                break;
                
            default:
                effectiveIndex = localIndex;
                break;
        }
        
        // effectiveIndexに基づいてサイズを調整
        float sizeMultiplier = 1.0f - (currentModifier * effectiveIndex);
        
        // 安全チェック：サイズが極小になるのを防ぐ
        sizeMultiplier = Mathf.Max(sizeMultiplier, 0.001f); // 最小0.1%のサイズを保証
        
        // 基本サイズ
        float baseWidth = 0.4f * sizeMultiplier;
        float baseHeight = 1.1f * currentLength * sizeMultiplier;
        
        // currentEdgeに基づいて先端の幅を計算
        float tipWidth = baseWidth * currentEdge;
        
        // 安全チェック：幅が0になるのを防ぐ
        baseWidth = Mathf.Max(baseWidth, 0.001f);
        tipWidth = Mathf.Max(tipWidth, 0.001f);
        
        // 頂点を計算（羽の形状）
        Vector3[] vertices = new Vector3[]
        {
            // 表面
            new Vector3(-baseWidth * 0.5f, -baseHeight * 0.55f, 0),  // 左下（根元）
            new Vector3(baseWidth * 0.5f, -baseHeight * 0.55f, 0),   // 右下（根元）
            new Vector3(tipWidth * 0.5f, baseHeight * 0.45f, 0),     // 右上（先端）
            new Vector3(-tipWidth * 0.5f, baseHeight * 0.45f, 0),    // 左上（先端）
            // 裏面（同じ頂点を複製）
            new Vector3(-baseWidth * 0.5f, -baseHeight * 0.55f, 0),  // 左下（根元）
            new Vector3(baseWidth * 0.5f, -baseHeight * 0.55f, 0),   // 右下（根元）
            new Vector3(tipWidth * 0.5f, baseHeight * 0.45f, 0),     // 右上（先端）
            new Vector3(-tipWidth * 0.5f, baseHeight * 0.45f, 0)     // 左上（先端）
        };
        
        // 三角形（表面と裏面の両方）
        int[] triangles = new int[] { 
            // 表面
            0, 2, 1, 0, 3, 2,
            // 裏面（逆順）
            4, 5, 6, 4, 6, 7
        };
        
        // UV座標
        Vector2[] uv = new Vector2[]
        {
            // 表面
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            // 裏面
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        
        return mesh;
    }

    /// <summary>
    /// BladeSplitModeを直接設定する（splitモード用）
    /// </summary>
    public void SetBladeSplitMode(BladeSplitMode mode)
    {
        if (bladeSplitMode != mode)
        {
            bladeSplitMode = mode;
            RecreateWingMeshes();
        }
    }

    /// <summary>
    /// HTTP経由で羽の色を設定する
    /// </summary>
    /// <param name="normal">通常時の色モード</param>
    /// <param name="animation">アニメーション時の色モード</param>
    /// <param name="hoverNoCmd">ホバー時（コマンド無）の色モード</param>
    /// <param name="hoverWithCmd">ホバー時（コマンド有）の色モード</param>
    public void SetColorsViaHttp(string normal, string animation, string hoverNoCmd, string hoverWithCmd)
    {
        Debug.Log($"[WingMenu] SetColorsViaHttp called - normal: {normal}, animation: {animation}, hoverNoCmd: {hoverNoCmd}, hoverWithCmd: {hoverWithCmd}");
        
        normalColorMode = normal ?? "white";
        animationColorMode = animation ?? "white";
        hoverNoCommandColorMode = hoverNoCmd ?? "lightblue";
        hoverWithCommandColorMode = hoverWithCmd ?? "yellow";
        
        // 色設定を即座に反映
        UpdateWingColors();
    }

    /// <summary>
    /// 羽の色を更新する
    /// </summary>
    private void UpdateWingColors()
    {
        if (!isMenuOpen || wingItems == null) return;
        
        for (int i = 0; i < wingItems.Count; i++)
        {
            if (wingItems[i].wingObject == null) continue;
            
            var renderer = wingItems[i].wingObject.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.material == null) continue;
            
            Color targetColor = GetWingColor(i);
            renderer.material.color = targetColor;
            
            // VRM読み込み後は色変更時もEmissionを維持
            if (vrmLoader != null && vrmLoader.LoadedModel != null && hoveredIndex != i)
            {
                // ホバー中でない場合は基本のEmissionを維持
                ApplyBrightnessEmission(renderer.material);
            }
        }
    }

    /// <summary>
    /// 指定された羽のインデックスに対する適切な色を取得する
    /// </summary>
    /// <param name="wingIndex">羽のインデックス</param>
    /// <returns>適用すべき色</returns>
    private Color GetWingColor(int wingIndex)
    {
        // ホバー中の場合
        if (hoveredIndex == wingIndex)
        {
            bool hasCommand = HasCommand(wingIndex);
            string colorMode = hasCommand ? hoverWithCommandColorMode : hoverNoCommandColorMode;
            
            if (colorMode == "gaming")
            {
                return GetRainbowColor(wingIndex, Time.time);
            }
            return ColorModeMap.ContainsKey(colorMode) ? ColorModeMap[colorMode] : Color.white;
        }
        
        // アニメーション中の場合
        if (isAnimating)
        {
            if (animationColorMode == "gaming")
            {
                return GetRainbowColor(wingIndex, Time.time);
            }
            return ColorModeMap.ContainsKey(animationColorMode) ? ColorModeMap[animationColorMode] : Color.white;
        }
        
        // 通常時
        if (normalColorMode == "gaming")
        {
            return GetRainbowColor(wingIndex, Time.time);
        }
        return ColorModeMap.ContainsKey(normalColorMode) ? ColorModeMap[normalColorMode] : Color.white;
    }

    /// <summary>
    /// 指定された羽にコマンドが定義されているかを判定する
    /// </summary>
    /// <param name="wingIndex">羽のインデックス</param>
    /// <returns>コマンドが定義されている場合はtrue</returns>
    private bool HasCommand(int wingIndex)
    {
        if (wingIndex < 0 || wingIndex >= wingItems.Count) return false;
        
        string label = wingItems[wingIndex].label;
        
        // Built-in functionsはコマンド有り
        if (WingMenuCommandHandler.IsBuiltinFunction(label)) return true;
        
        // placeholderはコマンド無し
        if (label == "placeholder") return false;
        
        // その他のカスタムメニューはコマンド有り
        return true;
    }

    /// <summary>
    /// VRM読み込み後の色のくすみ対策：全ての羽にEmissionを適用
    /// </summary>
    private void ApplyPostVRMEmission()
    {
        Debug.Log("[WingMenu] Applying post-VRM emission to brighten wings");
        
        if (wingItems == null) return;
        
        for (int i = 0; i < wingItems.Count; i++)
        {
            if (wingItems[i].wingObject == null) continue;
            
            var renderer = wingItems[i].wingObject.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.material == null) continue;
            
            // VRM読み込み後の明度補正用のEmissionを適用
            ApplyBrightnessEmission(renderer.material);
        }
    }
    
    /// <summary>
    /// 明度補正用のEmissionを適用する
    /// </summary>
    /// <param name="material">対象のマテリアル</param>
    private void ApplyBrightnessEmission(Material material)
    {
        if (material == null) return;
        
        // Emissionを有効にして適切な明度の白色光を追加
        material.EnableKeyword("_EMISSION");
        
        // RGB値180,180,180相当のEmissionを設定（255で割って正規化: 180/255 ≈ 0.706）
        Color emissionColor = new Color(0.706f, 0.706f, 0.706f, 1.0f);
        material.SetColor("_EmissionColor", emissionColor);
        
        Debug.Log($"[WingMenu] Applied brightness emission to material: {material.name}, emission: {emissionColor} (RGB: 180,180,180)");
    }

    #endregion
}
