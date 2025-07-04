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
    }

    void Update() {
        if (Application.isEditor || transparentWindow == null || !transparentWindow.IsAllowDragObjects()) {
            return; // 透過ドラッグが許可されてない場合は無効
        }

        // === (1) 左ドラッグでウィンドウ移動 ===
        if (Input.GetMouseButtonDown(0)) {
            isDragging = true;
            GetCursorPos(out dragStartCursor);
            GetWindowRect(GetActiveWindow(), out dragStartWindow);
        }
        else if (Input.GetMouseButtonUp(0)) {
            isDragging = false;
        }

        if (isDragging) {
            GetCursorPos(out POINT currentCursor);
            int dx = currentCursor.x - dragStartCursor.x;
            int dy = currentCursor.y - dragStartCursor.y;

            MoveWindow(
                GetActiveWindow(),
                dragStartWindow.left + dx,
                dragStartWindow.top + dy,
                dragStartWindow.right - dragStartWindow.left,
                dragStartWindow.bottom - dragStartWindow.top,
                true
            );
        }

        // === (2) 右ドラッグリサイズ ===
        if (Input.GetMouseButtonDown(1)) {
            isResizingRight = true;
            GetCursorPos(out resizeStartCursor);
            GetWindowRect(GetActiveWindow(), out resizeStartWindow);

            // [CDK-03020] リサイズ開始時、前回枠が残ってたら消す
            EraseFocusRectIfNeeded();
        }
        else if (Input.GetMouseButtonUp(1)) {
            // リサイズ終了
            isResizingRight = false;

            // [CDK-03030] マウスアップしたら、枠を消す
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

            // (a) 枠を表示する用の仮Rect
            RECT curFocusRect = new RECT {
                left = left,
                top = top,
                right = left + newW,
                bottom = top + newH
            };

            // [CDK-03040] 前フレームの枠を消し → 今フレームの枠を表示
            UpdateFocusRect(curFocusRect);

            // (b) 実際に MoveWindow するならここ
            MoveWindow(GetActiveWindow(), left, top, newW, newH, true);
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
}
