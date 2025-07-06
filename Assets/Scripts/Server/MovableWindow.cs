using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class MovableWindow : MonoBehaviour {
    // === Win32 API ===
    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    // [CDK-03001] 枠線用に追加
    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    static extern int DrawFocusRect(IntPtr hDC, ref RECT lprc);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    private bool isDragging = false;
    private POINT dragStartCursor;
    private RECT dragStartWindow;

    private ServerConfig config;
    private TransparentWindow transparentWindow;
    private WingMenuSystem wingMenuSystem;

    // [CDK-01001] 右クリックリサイズフラグ（前回と同じ）
    private bool isResizingRight = false;
    private POINT resizeStartCursor;
    private RECT resizeStartWindow;

    // [CDK-03010] ★枠線を描画するための管理変数
    private bool prevFocusRectValid = false;  // 前回フレームに枠を引いたかどうか
    private RECT prevFocusRect;               // 前回描画した枠

    void Start() {
        if (Application.isEditor) return;

        // config.json を読み込み
        var config = ServerConfig.Instance;
        // 初期設定は TransparentWindow にまかせる

        // TransparentWindow を取得（allowDragObjects 状態確認用）
        transparentWindow = FindFirstObjectByType<TransparentWindow>();
        
        // WingMenuSystem を取得（競合回避用）
        wingMenuSystem = FindFirstObjectByType<WingMenuSystem>();
    }

    void Update() {
        if (Application.isEditor || transparentWindow == null || !transparentWindow.IsAllowDragObjects()) {
            return; // 透過ドラッグが許可されてない場合は無効
        }

        // メニューの状態を確認
        bool menuOpen = wingMenuSystem != null && wingMenuSystem.IsMenuOpen();
        
        if (menuOpen) {
            // メニューが開いている時: WingMenuSystemが処理完了するまで待機
            if (wingMenuSystem != null && wingMenuSystem.HasProcessedClick()) {
                return; // WingMenuSystemがクリックを処理済み
            }
            
            // 左ドラッグで移動（枠線付き）- ただしWingMenuSystemが処理しない場合のみ
            HandleDragMove();
        } else {
            // メニューが閉じている時: 左ドラッグでリサイズ
            HandleDragResize();
        }
    }

    /// <summary>
    /// [CDK-03050] 前フレームで描画した焦点枠を消して、今回の枠を描画
    /// </summary>
    private void UpdateFocusRect(RECT newRect) {
        // デスクトップ全体に対して描画したいなら GetDC(IntPtr.Zero)
        IntPtr hdc = GetDC(IntPtr.Zero);

        // 1) 前回枠があれば XOR で消す
        if (prevFocusRectValid) {
            DrawFocusRect(hdc, ref prevFocusRect);
        }

        // 2) 新枠を描画
        DrawFocusRect(hdc, ref newRect);

        ReleaseDC(IntPtr.Zero, hdc);

        // 更新
        prevFocusRect = newRect;
        prevFocusRectValid = true;
    }

    /// <summary>
    /// [CDK-03060] 残っている枠があれば消す
    /// </summary>
    private void EraseFocusRectIfNeeded() {
        if (!prevFocusRectValid) return;

        IntPtr hdc = GetDC(IntPtr.Zero);

        // XORをもう一度呼ぶと消える
        DrawFocusRect(hdc, ref prevFocusRect);

        ReleaseDC(IntPtr.Zero, hdc);

        prevFocusRectValid = false;
    }

    /// <summary>
    /// メニューが開いている時の左ドラッグ移動処理（枠線付き）
    /// </summary>
    private void HandleDragMove() {
        if (Input.GetMouseButtonDown(0)) {
            // 羽の上でクリックした場合はドラッグを開始しない
            if (IsClickOnWing()) {
                return;
            }
            
            isDragging = true;
            GetCursorPos(out dragStartCursor);
            GetWindowRect(GetActiveWindow(), out dragStartWindow);

            // 移動開始時、前回枠が残ってたら消す
            EraseFocusRectIfNeeded();
        }
        else if (Input.GetMouseButtonUp(0)) {
            isDragging = false;

            // 移動終了時、枠を消す
            EraseFocusRectIfNeeded();
        }

        if (isDragging) {
            GetCursorPos(out POINT currentCursor);
            int dx = currentCursor.x - dragStartCursor.x;
            int dy = currentCursor.y - dragStartCursor.y;

            // 新しい位置を計算
            int newLeft = dragStartWindow.left + dx;
            int newTop = dragStartWindow.top + dy;
            int width = dragStartWindow.right - dragStartWindow.left;
            int height = dragStartWindow.bottom - dragStartWindow.top;

            // 移動用の枠を表示
            RECT curFocusRect = new RECT {
                left = newLeft,
                top = newTop,
                right = newLeft + width,
                bottom = newTop + height
            };

            // 前フレームの枠を消し → 今フレームの枠を表示
            UpdateFocusRect(curFocusRect);

            // 実際にウィンドウを移動
            MoveWindow(GetActiveWindow(), newLeft, newTop, width, height, true);
        }
    }

    /// <summary>
    /// メニューが閉じている時の左ドラッグリサイズ処理
    /// </summary>
    private void HandleDragResize() {
        if (Input.GetMouseButtonDown(0)) {
            isResizingRight = true;
            GetCursorPos(out resizeStartCursor);
            GetWindowRect(GetActiveWindow(), out resizeStartWindow);

            // リサイズ開始時、前回枠が残ってたら消す
            EraseFocusRectIfNeeded();
        }
        else if (Input.GetMouseButtonUp(0)) {
            // リサイズ終了
            isResizingRight = false;

            // マウスアップしたら、枠を消す
            EraseFocusRectIfNeeded();
        }

        if (isResizingRight) {
            // リサイズ用の新しい枠を計算
            GetCursorPos(out POINT cur2);

            int dx2 = cur2.x - resizeStartCursor.x;
            int dy2 = cur2.y - resizeStartCursor.y;

            int startW = resizeStartWindow.right - resizeStartWindow.left;
            int startH = resizeStartWindow.bottom - resizeStartWindow.top;

            int newW = startW + dx2;
            int newH = startH + dy2;

            // 最小200×200
            if (newW < 200) newW = 200;
            if (newH < 200) newH = 200;

            // 最大はモニタ解像度
            int maxW = Screen.currentResolution.width;
            int maxH = Screen.currentResolution.height;
            if (newW > maxW) newW = maxW;
            if (newH > maxH) newH = maxH;

            // 左上固定
            int left = resizeStartWindow.left;
            int top = resizeStartWindow.top;

            // リサイズ用の枠を表示
            RECT curFocusRect = new RECT {
                left = left,
                top = top,
                right = left + newW,
                bottom = top + newH
            };

            // 前フレームの枠を消し → 今フレームの枠を表示
            UpdateFocusRect(curFocusRect);

            // 実際にウィンドウをリサイズ
            MoveWindow(GetActiveWindow(), left, top, newW, newH, true);
        }
    }

    /// <summary>
    /// 現在のマウス位置が羽の上かどうかをチェック
    /// </summary>
    private bool IsClickOnWing() {
        if (wingMenuSystem == null || !wingMenuSystem.IsMenuOpen()) {
            return false;
        }

        // WingMenuSystemのレイキャスト処理と同じ方法でチェック
        Camera mainCamera = Camera.main;
        if (mainCamera == null) {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // UIレイヤー（メニューレイヤー）でレイキャストを実行
        int menuLayer = LayerMask.NameToLayer("UI");
        if (menuLayer == -1) menuLayer = 0; // フォールバック
        
        return Physics.Raycast(ray, out hit, 100f, 1 << menuLayer);
    }
}
