using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections;
/*
using UnityEditor.PackageManager.UI;
*/

public class TransparentWindow : MonoBehaviour {
    // === Win32 API 定義 ===
    [DllImport("user32.dll")]
    static extern IntPtr SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);
    const int SM_CXVIRTUALSCREEN = 78;
    const int SM_CYVIRTUALSCREEN = 79;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_SHOWWINDOW = 0x0040;
    const uint SWP_FRAMECHANGED = 0x0020;

    // === Win32 定数 ===
    const int GWL_EXSTYLE = -20;
    const int WS_EX_LAYERED = 0x80000;
    const int WS_EX_TRANSPARENT = 0x20;
    const uint LWA_COLORKEY = 0x1;
    const uint LWA_ALPHA = 0x2;

    const int GWL_STYLE = -16;
    const uint WS_VISIBLE = 0x10000000;
    const uint WS_POPUP = 0x80000000;
    const uint WS_OVERLAPPEDWINDOW = 0x00CF0000; // 通常ウィンドウ（枠＋タイトルバーあり）

    private IntPtr hwnd;

    // 状態保持
    private bool _lastEnabledState;

    // config 参照
    private ServerConfig config;

    // デフォルトの透過色 (マゼンタ)
    private Color32 transparentColor = new Color32(255, 0, 255, 255);

    private bool allowDragObjects = false;
    public bool IsAllowDragObjects() => allowDragObjects;

    private bool appliedMagicInitialSize = false;
    private int savedPosX, savedPosY, savedWidth, savedHeight;
    void Awake() {
        // 可能な限り早くこのスクリプトを実行
        // Awakeで初期ウィンドウスタイルを設定
        if (!Application.isEditor) {
            hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd != IntPtr.Zero) {
                int newStyle = unchecked((int)(WS_POPUP | WS_VISIBLE));
                SetWindowLong(hwnd, GWL_STYLE, newStyle);
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                Debug.Log("🪟 Initial window style set to POPUP (frameless) in Awake");

                // ★ magic 対応
                var c = ServerConfig.Instance;
                if (c != null && c.Window != null && c.Window.magic) {
                    var w = c.Window.position;
                    savedPosX = w.x;
                    savedPosY = w.y;
                    savedWidth = w.width;
                    savedHeight = w.height;

                    MoveWindow(hwnd, -2000, -2000, 1, 1, true);
                    Debug.Log("★ magic=true → Awake時点でウィンドウを(1x1)にしました！");
                    appliedMagicInitialSize = true;
                }
            }
        }
    }

    void Start() {
        config = ServerConfig.Instance;

        if (hwnd == IntPtr.Zero) {
            hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        }

        // ✅ まずカメラ背景を即設定（クロマキー対応）
        Camera cam = Camera.main;
        if (cam != null) {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = transparentColor;
            Debug.Log("✅ カメラ背景即設定");
        }

        // ✅ エディタでもビルドでも最初に ApplyWindowSettings を呼ぶ
        if (!Application.isEditor) {
            ApplyWindowSettings();

            StartCoroutine(DelayApply());
        }
    }

    IEnumerator DelayApply() {
        yield return null;
        //yield return new WaitForSeconds(0.1f);

        if (config?.Window?.borderless == false) {
            SetWindowLong(hwnd, GWL_STYLE, (int)(WS_OVERLAPPEDWINDOW | WS_VISIBLE));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            Debug.Log("🪟 枠ありに切り替えました！（borderless=false）");

            // Unity 側の「最大化状態」を解除
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
            Debug.Log("📏 Unity側の最大化モード解除 (Windowed に強制)");
        }
        else {
            Debug.Log("🪟 borderless=true or undefined → Unityのデフォルト枠なしを維持");
        }

        ApplyWindowSettings(); // 透過など

        if (appliedMagicInitialSize) {
            MoveWindow(hwnd, savedPosX, savedPosY, savedWidth, savedHeight, true);
            Debug.Log($"★ magic=true → 透明化完了後にサイズを戻しました！({savedWidth}x{savedHeight})");
        }

    }

    public void OverrideTransparentColor(Color newColor) {
        transparentColor = (Color32)newColor; // ← byte精度で確定させる！

        string hex = $"#{ColorUtility.ToHtmlStringRGB(transparentColor)}";
        config.Window.transparentColor = hex;

        Debug.Log($"✅ OverrideTransparentColor: transparentColor={transparentColor} (hex={hex})");
    }

    // 有効/無効をトリガーに透過処理を更新
    private void UpdateTransparency() {
        SetTransparent(enabled);
    }

    /// <summary>
    /// ウィンドウを透過/非透過に切り替える（WinAPI制御）
    /// </summary>
    public void SetTransparent(bool transparent, bool pointerEventsNone = false) {
        if (Application.isEditor || hwnd == IntPtr.Zero) return;

        ApplyTransparentColorFromConfig();

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (transparent) {
            // 透過処理 (透明化時)
            exStyle |= WS_EX_LAYERED;
            exStyle = pointerEventsNone ? exStyle | WS_EX_TRANSPARENT : exStyle & ~WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            uint colorKey = ((uint)transparentColor.b << 16) | ((uint)transparentColor.g << 8) | transparentColor.r;
            SetLayeredWindowAttributes(hwnd, colorKey, 0, LWA_COLORKEY);

            // ✅ Canvasを無効化（透明時）
            var canvas = GameObject.FindObjectOfType<Canvas>();
            if (canvas != null && canvas.enabled) {
                canvas.enabled = false;
                Debug.Log("✅ Canvas disabled for transparency ON.");
            }
        }
        else {
            // 非透過処理 (透明解除時)
            exStyle |= WS_EX_LAYERED;
            exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);

            // ✅ 透明解除時にCanvasを再度有効化する（追加！）
            var canvas = GameObject.FindObjectOfType<Canvas>();
            if (canvas != null && !canvas.enabled) {
                canvas.enabled = true;
                Debug.Log("✅ Canvas re-enabled for transparency OFF.");
            }
        }

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    public void SetAllowDragObjects(bool allow) {
        this.allowDragObjects = allow;
        Debug.Log($"🖱️ allowDragObjects set to: {allow}");

        if (hwnd == IntPtr.Zero) {
            Debug.LogWarning("⚠️ hwnd が取得できてないっぽいよ！");
            return;
        }

        int style = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (allow) {
            // クリック可能にする（マウスイベント受ける）
            SetWindowLong(hwnd, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }
        else {
            // クリックを透過（マウスイベント通す＝クリック受けない）
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);
        }
    }

    /// <summary>
    /// config.json の transparentColor を Color32 に変換
    /// </summary>
    private void ApplyTransparentColorFromConfig() {
        string hex = config?.Window?.transparentColor ?? "#FF00FF";
        if (ColorUtility.TryParseHtmlString(hex, out Color parsed)) {
            transparentColor = parsed;
        }
        else {
            Debug.LogWarning($"Invalid transparentColor in config.json: {hex}");
            transparentColor = new Color32(255, 0, 255, 255); // fallback: マゼンタ
        }
    }

    private void ApplyWindowSettings() {
        var win = config.Window;

        Color parsedColor = Color.magenta;
        bool isColorValid = ColorUtility.TryParseHtmlString(win.transparentColor, out parsedColor);

        if (win.transparent) {
            if (isColorValid) {
                OverrideTransparentColor(parsedColor);
            }
            else {
                Debug.LogError($"Invalid transparentColor specified: {win.transparentColor}");
                parsedColor = Color.magenta;
            }
        }

        if (!Application.isEditor) {
            SetTransparent(win.transparent, win.pointerEventsNone);
            SetStayOnTop(win.stayOnTop);

            if (win.saveWindowBounds && win.position != null) {
                MoveToConfiguredPosition(
                    win.position.x,
                    win.position.y,
                    win.position.width,
                    win.position.height,
                    win.monitorIndex
                );
            }
        }

        if (win.transparent) {
            Camera mainCam = Camera.main;
            if (mainCam != null) {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = parsedColor;
            }
            else {
                Debug.LogError("Main Camera not found.");
            }

            var canvas = GameObject.FindObjectOfType<Canvas>();
            if (canvas != null) {
                canvas.enabled = false;
                Debug.Log("✅ Canvas disabled to ensure transparency.");
            }
        }

        SetAllowDragObjects(win.allowDragObjects); // Editorでも意味あり
    }

    /// <summary>
    /// ウィンドウを config の位置・サイズ
    /// </summary>
    private void MoveToConfiguredPosition(int x, int y, int width, int height, int monitorIndex) {
        if (config?.Window?.borderless == false) {
            return;
        }

        // 仮想スクリーン全体のサイズを取得（全モニターを含む領域）
        int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int virtualHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        // ウィンドウが画面外に出ていないか判定（左上と右下でチェック）
        bool outOfBounds =
            x < 0 || y < 0 ||
            (x + width) > virtualWidth ||
            (y + height) > virtualHeight;

        if (outOfBounds) {
            Debug.LogWarning($"💥 ウィンドウが画面外に出てるっぽいよ！補正するにゃん～ (virtualSize={virtualWidth}x{virtualHeight})");

            // 仮想スクリーンの中央に補正
            x = (virtualWidth - width) / 2;
            y = (virtualHeight - height) / 2;
        }

        MoveWindow(hwnd, x, y, width, height, true);
    }

    public void SetStayOnTop(bool enable) {
        if (Application.isEditor || hwnd == IntPtr.Zero) return;

        SetWindowPos(hwnd,
            enable ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        Debug.Log($"🪟 StayOnTop set to: {enable}");
    }

    public void SaveWindowBoundsIfNeeded() {
        if (Application.isEditor || config?.Window?.saveWindowBounds != true)
            return;

        if (!GetWindowRect(hwnd, out RECT rect)) return;

        config.Window.position.x = rect.left;
        config.Window.position.y = rect.top;
        config.Window.position.width = rect.right - rect.left;
        config.Window.position.height = rect.bottom - rect.top;
        
        /*
        config.SaveConfigPreserveUnknown();
        try {
            string path = UserPaths.ConfigPath;

            // データの読み込みに失敗しても空インスタンスで代替
            ServerConfigData data;
            try {
                var current = File.ReadAllText(path);
                data = JsonUtility.FromJson<ServerConfigData>(current) ?? new ServerConfigData();
            }
            catch {
                Debug.LogWarning("config.json の読み込みに失敗 → 空データで初期化");
                data = new ServerConfigData();
            }

            data.window = config.Window;
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log("✅ config.json に位置情報を保存したよ！");
        }
        catch (Exception ex) {
            Debug.LogError($"❌ config.json 保存失敗: {ex.Message}");
        }
        */
        // 位置情報を in-memory の config に反映済みなので
        config.SaveConfigPreserveUnknown();   // 未知キー保持で安全保存        
    }

    private void OnEnable() {
        if (!Application.isEditor) {
            UpdateTransparency();
        }
    }

    private void OnDisable() {
        if (!Application.isEditor) {
            UpdateTransparency();
        }
    }
}