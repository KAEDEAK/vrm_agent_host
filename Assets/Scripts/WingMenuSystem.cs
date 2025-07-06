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

    // レイヤー設定
    private const string MENU_LAYER_NAME = "UI";
    private int menuLayer;

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
        // アニメーション中は入力を受け付けない
        if (isAnimating) return;

        // 左クリック処理
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[WingMenu] Mouse button down detected");
            HandleMouseClick();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            // マウスボタンが離されたらフラグをリセット
            hasProcessedClick = false;
        }
        
        // 右クリックでメニュー表示（MovableWindowと競合しない）
        if (Input.GetMouseButtonDown(1) && !isMenuOpen)
        {
            Debug.Log("[WingMenu] Right click - showing menu");
            ShowMenu();
        }

        // ホバー処理
        HandleHover();

        // 初回表示：アバターがない時、またはアバターが読み込まれた直後
        if (!isMenuOpen && !hasEverBeenClosed)
        {
            // 羽だけを画面中央に表示
            ShowMenuAtCenter();
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
        // 左側の羽を4個作成
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
            
            // 機能割り当て
            wingItem.label = $"Menu {i + 1}";
            int capturedIndex = i;  // ラムダ式用にインデックスをキャプチャ
            wingItem.onClick = () => OnPlaceholderClick(capturedIndex);
            
            wingItems.Add(wingItem);
        }
        
        // 右側の羽を4個作成
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
            
            // 機能割り当て
            if (i == 0) // 右下（右側の1番目）がEXIT
            {
                wingItem.label = "EXIT";
                wingItem.onClick = OnExitClick;
                
                // EXITの羽は赤っぽく
                var renderer = wingItem.wingObject.GetComponent<MeshRenderer>();
                renderer.material.color = exitColor;
            }
            else
            {
                wingItem.label = $"Menu {i + 4}";  // 右側は4番目から（EXITが5番目になるため）
                int capturedIndex = i + 3;  // ラムダ式用にインデックスをキャプチャ（EXITの分を調整）
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
        
        // マテリアル設定（確実に表示されるシェーダーを使用）
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
        
        meshRenderer.material = material;
        
        // レンダリング順序を強制的に設定
        meshRenderer.sortingOrder = 100; // 高い値で前面に表示
        
        // デバッグ情報
        Debug.Log($"[WingMenu] Created wing: {name}, layer: {wing.layer} ({LayerMask.LayerToName(wing.layer)})");
        
        // コライダー追加（クリック検出用）
        BoxCollider collider = wing.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.6f * wingScale, 1.0f * wingScale, 0.1f);
        
        return wing;
    }

    private void HandleMouseClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // 羽のクリックチェック
        if (isMenuOpen)
        {
            Debug.Log("[WingMenu] Menu is open, checking wing clicks");
            if (Physics.Raycast(ray, out hit, 100f, 1 << menuLayer))
            {
                Debug.Log($"[WingMenu] Hit object: {hit.collider.gameObject.name}");
                // 羽がクリックされた
                for (int i = 0; i < wingItems.Count; i++)
                {
                    if (hit.collider.gameObject == wingItems[i].wingObject)
                    {
                        Debug.Log($"[WingMenu] Wing {i} clicked");
                        wingItems[i].onClick?.Invoke();
                        hasProcessedClick = true;
                        return;
                    }
                }
            }
            else
            {
                // メニュー外をクリックしたら閉じる
                Debug.Log("[WingMenu] Clicked outside menu, closing");
                HideMenu();
                hasProcessedClick = true;
                return;
            }
        }
        
        // アバターのクリックチェック（シングルクリック）
        if (vrmLoader != null && vrmLoader.LoadedModel != null)
        {
            Debug.Log("[WingMenu] Checking avatar click");
            
            // まず全てのヒットをチェック
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            Debug.Log($"[WingMenu] RaycastAll hit {hits.Length} objects");
            
            foreach (var h in hits)
            {
                Debug.Log($"[WingMenu] Hit: {h.collider.gameObject.name} (layer: {h.collider.gameObject.layer})");
                if (IsAvatarObject(h.collider.gameObject))
                {
                    Debug.Log("[WingMenu] Avatar clicked! Toggling menu");
                    ToggleMenu();
                    hasProcessedClick = true;
                    return;
                }
            }
            
            Debug.Log("[WingMenu] No avatar hit detected");
        }
        else
        {
            Debug.Log($"[WingMenu] Avatar not ready - vrmLoader: {vrmLoader != null}, LoadedModel: {vrmLoader?.LoadedModel != null}");
        }
        
        // どこにもヒットしなかった場合は、MovableWindowに処理を任せる
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
            // 前のホバーを解除
            if (hoveredIndex >= 0)
            {
                var renderer = wingItems[hoveredIndex].wingObject.GetComponent<MeshRenderer>();
                renderer.material.color = (hoveredIndex == 4) ? exitColor : wingColor;  // EXITは5番目（インデックス4）
            }
            
            // 新しいホバーを適用
            if (newHoveredIndex >= 0)
            {
                var renderer = wingItems[newHoveredIndex].wingObject.GetComponent<MeshRenderer>();
                renderer.material.color = hoverColor;
            }
            
            hoveredIndex = newHoveredIndex;
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
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
}
