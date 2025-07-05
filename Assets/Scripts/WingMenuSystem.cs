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
    private Color wingColor = new Color(0.3f, 0.7f, 1f, 1f);  // 明るい青色に変更
    private Color hoverColor = new Color(1f, 1f, 0.3f, 1f);   // 黄色に変更
    private Color exitColor = new Color(1f, 0.3f, 0.3f, 1f);  // 明るい赤色

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
        
        // 初期状態では非表示
        HideMenuImmediate();
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
    }

    private void CreateMenuContainer()
    {
        menuContainer = new GameObject("WingMenuContainer");
        menuContainer.transform.SetParent(transform);
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
                -2.0f  // Z座標を前面に変更
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
                -2.0f  // Z座標を前面に変更
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
        
        // 羽の形状を作成（シンプルな四角形を変形）
        Mesh mesh = new Mesh();
        
        // 頂点（羽の形に変形した四角形 - より羽らしい形状に）
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.2f, -0.6f, 0) * wingScale,  // 左下（根元）
            new Vector3(0.2f, -0.6f, 0) * wingScale,   // 右下（根元）
            new Vector3(0.3f, 0.5f, 0) * wingScale,    // 右上（先端）
            new Vector3(-0.3f, 0.5f, 0) * wingScale    // 左上（先端）
        };
        
        // 三角形
        int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        
        // UV座標
        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
        
        // マテリアル設定（より確実なシェーダーに変更）
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
            Debug.LogWarning("[WingMenu] Unlit/Color shader not found, using Standard");
        }
        
        Material material = new Material(shader);
        material.color = wingColor;
        meshRenderer.material = material;
        
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
        
        // カメラの前にメニューを配置
        if (mainCamera != null)
        {
            // カメラの前方3ユニットの位置に配置
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 menuPosition = mainCamera.transform.position + cameraForward * 3.0f;
            menuContainer.transform.position = menuPosition;
            
            // メニューをカメラに向ける
            menuContainer.transform.LookAt(mainCamera.transform);
            menuContainer.transform.rotation = Quaternion.LookRotation(-cameraForward);
            
            Debug.Log($"[WingMenu] Menu positioned in front of camera: {menuPosition}");
            Debug.Log($"[WingMenu] Camera position: {mainCamera.transform.position}, forward: {cameraForward}");
        }
        else
        {
            // カメラがない場合は原点に配置
            menuContainer.transform.position = Vector3.zero;
            Debug.Log("[WingMenu] No camera found, menu at origin");
        }
        
        // デバッグ：羽の状態を確認（アニメーション前）
        Debug.Log($"[WingMenu] Menu container active: {menuContainer.activeSelf}");
        Debug.Log($"[WingMenu] Wing count: {wingItems.Count}");
        for (int i = 0; i < wingItems.Count; i++)
        {
            var wing = wingItems[i];
            Debug.Log($"[WingMenu] Wing {i} BEFORE animation: active={wing.wingObject.activeSelf}, " +
                     $"pos={wing.wingObject.transform.position}, " +
                     $"scale={wing.wingObject.transform.localScale}");
        }
        
        // テスト：アニメーションなしで即座に表示
        bool useAnimation = false; // falseにするとアニメーションなし
        if (useAnimation)
        {
            StartCoroutine(AnimateMenuOpen());
        }
        else
        {
            // アニメーションなしで即座に表示
            ShowMenuImmediate();
        }
    }

    private void ShowMenuAtCenter()
    {
        if (isAnimating || isMenuOpen) return;
        
        isMenuOpen = true;
        menuContainer.SetActive(true);
        
        // 画面中央に配置
        menuContainer.transform.position = new Vector3(0, 0, 0);
        
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
}
