using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AnimationServer : MonoBehaviour {
    public static AnimationServer Instance { get; private set; }

    private HttpListener httpListener;
    private HttpListener httpsListener;
    private Thread httpThread;
    private Thread httpsThread;
    public bool IsHttpListening => httpListener != null && httpListener.IsListening;
    public bool IsHttpsListening => httpsListener != null && httpsListener.IsListening;

    // ローカルで保持する設定値は、主に高速アクセス用にコピーしているが、
    // 基本は ServerConfig.Instance から取得することを前提としています。
    private int httpPort = 34560;
    private int httpsPort = 34561;
    private bool useHttp = true;
    private bool useHttps = false;
    private bool listenLocalhostOnly = true;
    //private List<string> allowedRemoteIPs = new List<string>();    
    private List<string> allowedRemoteIPs = new List<string> { "127.0.0.1", "::1" };
    private bool autoPrepareSeamless = false;
    public bool vsync = true;
    public int targetFramerate = 60;

    private volatile bool _serverStopping = false; // 停止フラグ

    // HttpListenerContext をスレッド安全にキューイングしてメインスレッドで処理する
    private ConcurrentQueue<HttpListenerContext> requestQueue = new ConcurrentQueue<HttpListenerContext>();

    // target=xxx で振り分ける各種コマンドハンドラ
    private Dictionary<string, IHttpCommandHandler> commandHandlers;

    #region sanitize

    private bool IsSafeQueryParam(string paramValue, int maxLength = 100) {
        if (string.IsNullOrEmpty(paramValue) || paramValue.Length > maxLength)
            return false;

        foreach (char c in paramValue) {
            if (!(('a' <= c && c <= 'z') ||
                  ('A' <= c && c <= 'Z') ||
                  ('0' <= c && c <= '9') ||
                  c == '-' || c == '_')) {
                return false;
            }
        }
        return true;
    }
    /*
    // 危険文字を排除する正規表現の別例(参考)
    private static readonly Regex SafeQueryParamRegex = new Regex(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);

    private bool IsSafeQueryParam(string paramValue, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(paramValue) || paramValue.Length > maxLength)
            return false;
        return SafeQueryParamRegex.IsMatch(paramValue);
    }
    */
    #endregion

    #region Unityライフサイクル

    private void Awake() {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
            return;
#endif
        Instance = this;
        Application.runInBackground = true;
        LoadConfig();  // ServerConfig.Instance から設定をロード
        i18nMsg.InitializeLocalization();
    }

#if UNITY_EDITOR
    private void OnEnable() {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable() {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (state == PlayModeStateChange.ExitingPlayMode) {
            Debug.Log("👋 PlayMode exiting. Stopping server...");
            StopServer();
        }
    }
#endif

    private void Start() {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
            return;
#endif
        if (!Application.isPlaying)
            return;

        // 初期化は ServerConfig の値を反映
        Application.targetFrameRate = targetFramerate;
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        Debug.Log($"FrameRate: {Application.targetFrameRate}, VSync: {(vsync ? "On" : "Off")}");

        // 各コンポーネント取得
        var animationHandler = UnityEngine.Object.FindAnyObjectByType<AnimationHandler>();
        var vrmLoader = UnityEngine.Object.FindAnyObjectByType<VRMLoader>();
        var lipSync = UnityEngine.Object.FindAnyObjectByType<AudioLipSync>();
        var imageLoader = UnityEngine.Object.FindAnyObjectByType<LocalImageLoader>();

        // コマンドごとにハンドラを登録
        commandHandlers = new Dictionary<string, IHttpCommandHandler> {
            { "animation",  new AnimationCommandHandler(animationHandler, vrmLoader) },
            { "lipSync",    new LipSyncCommandHandler(lipSync) },
            { "vrm",        new VrmCommandHandler(vrmLoader) },
            { "background", new BackgroundCommandHandler(imageLoader) },
            { "server",     new ServerCommandHandler() },
            { "camera",     new CameraCommandHandler(vrmLoader) },
            { "credits",    new CreditsCommandHandler() }
        };

        StartServer();
    }

    private void Update() {
        // リクエストキューに溜まっているものをメインスレッドで処理
        while (requestQueue.TryDequeue(out var context)) {
            ProcessRequestOnMainThread(context);
        }
    }

    private void OnApplicationQuit() {
        TrySaveWindowState();
        StopServer();
    }

    private void TrySaveWindowState() {
        var win = GameObject.FindFirstObjectByType<TransparentWindow>();
        if (win != null) {
            win.SaveWindowBoundsIfNeeded();
        }
    }

    // ★ 修正：もともと "listenerThread" を見てたが無いので httpListener / httpsListener だけチェック
    private void OnDestroy() {
        if (httpListener != null || httpsListener != null) {
            Debug.LogWarning("🧹 OnDestroy: Server still running!? Trying to stop...");
            StopServer();
        }
        Instance = null;
    }

    #endregion

    #region サーバー起動／停止・リスナー処理

    private void LoadConfig() {
        var config = ServerConfig.Instance;
        httpPort = config.httpPort != 0 ? config.httpPort : 34560;
        httpsPort = config.httpsPort != 0 ? config.httpsPort : 34561;
        useHttp = config.useHttp;
        useHttps = config.useHttps;
        listenLocalhostOnly = config.listenLocalhostOnly;
        allowedRemoteIPs = config.allowedRemoteIPs ?? new List<string>();
        autoPrepareSeamless = config.autoPrepareSeamless;
        vsync = config.vsync;
        targetFramerate = config.targetFramerate;

        Debug.Log($"[DEBUG] listenLocalhostOnly = {ServerConfig.Instance.listenLocalhostOnly}");
    }

    private void StartServer() {
        if (useHttp && httpListener != null && httpListener.IsListening) {
            Debug.LogWarning("⚠️ HTTP listener is already running. Skipping Start.");
            return;
        }
        if (useHttps && httpsListener != null && httpsListener.IsListening) {
            Debug.LogWarning("⚠️ HTTPS listener is already running. Skipping Start.");
            return;
        }
        try {
            // HTTP
            if (useHttp) {
                httpListener = new HttpListener();
                Debug.Log($"StartServer listenLocalhostOnly {listenLocalhostOnly}");

                if (listenLocalhostOnly) {
                    // Firefoxを考慮し http://+:{port}/ を使いながら、実質ローカル用
                    httpListener.Prefixes.Add($"http://+:{httpPort}/");
                    /*
                    下記は参考用：
                    httpsListener.Prefixes.Add($"https://localhost:{httpsPort}/");
                    httpsListener.Prefixes.Add($"https://127.0.0.1:{httpsPort}/");
                    httpsListener.Prefixes.Add($"https://[::1]:{httpsPort}/");
                    */
                    Debug.Log($"🟢 HTTP listening on port {httpPort} (localhost only)");
                }
                else {
                    httpListener.Prefixes.Add($"http://+:{httpPort}/");
                    Debug.Log($"🟢 HTTP listening on port {httpPort} (all addresses)");
                }

                // バインド状況をログ
                foreach (string prefix in httpListener.Prefixes) {
                    Debug.Log($"🔍 Bound HTTP Prefix: {prefix}");
                }

                httpListener.Start();
                httpThread = new Thread(() => HandleRequestsThread(httpListener));
                httpThread.Start();
            }

            // HTTPS
            if (useHttps) {
                httpsListener = new HttpListener();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                if (listenLocalhostOnly) {
                    httpsListener.Prefixes.Add($"https://+:{httpsPort}/");
                    /*
                    下記は参考用：
                    httpsListener.Prefixes.Add($"https://localhost:{httpsPort}/");
                    httpsListener.Prefixes.Add($"https://127.0.0.1:{httpsPort}/");
                    httpsListener.Prefixes.Add($"https://[::1]:{httpsPort}/");
                    */
                    Debug.Log($"🔒 HTTPS listening on port {httpsPort} (localhost only)");
                }
                else {
                    httpsListener.Prefixes.Add($"https://+:{httpsPort}/");
                    Debug.Log($"🔒 HTTPS listening on port {httpsPort} (all addresses)");
                }

                foreach (string prefix in httpsListener.Prefixes) {
                    Debug.Log($"🔍 Bound HTTPS Prefix: {prefix}");
                }

                httpsListener.Start();
                httpsThread = new Thread(() => HandleRequestsThread(httpsListener));
                httpsThread.Start();
            }

            Debug.Log("✅ AnimationServer started.");
        }
        catch (Exception e) {
            Debug.LogError($"❌ Failed to start server: {e}");
        }
    }

    private void StopServer() {
        _serverStopping = true;

        if (httpListener != null)
            StopListener(ref httpListener, ref httpThread, "HTTP");

        if (httpsListener != null)
            StopListener(ref httpsListener, ref httpsThread, "HTTPS");

        Debug.Log("🛑 AnimationServer stopped completely.");
    }

    private void StopListener(ref HttpListener listener, ref Thread thread, string label) {
        try {
            if (listener != null) {
                if (listener.IsListening) {
                    try { listener.Stop(); } catch { }
                    try { listener.Abort(); } catch { }
                    listener.Close();
                }
                
                Debug.Log($"❎ {label} listener stopped.");
            }
        }
        catch (Exception e) {
            Debug.LogError($"❌ {label} Stop Error: {e.Message}");
        }
        finally {
            listener = null;
        }

        if (thread != null && thread.IsAlive) {
            if (!thread.Join(3000)) {
                Debug.LogWarning($"⚠️ {label} listener thread did not terminate in a timely manner.");
            }
            thread = null;
        }
    }

    private void HandleRequestsThread(HttpListener listener) {
        /*
        try {
            while (!_serverStopping && listener.IsListening) {
                var asyncResult = listener.BeginGetContext(ListenerCallback, listener);
                asyncResult.AsyncWaitHandle.WaitOne();
            }
        }
        */
        try {
            while (!_serverStopping && listener.IsListening) {
                // Stop() すると GetContext() が例外で抜けるので OK
                var context = listener.GetContext();
                if (context != null) requestQueue.Enqueue(context);
            }
        }
        catch (HttpListenerException e) {
            // 停止/クローズ時によく発生するので除外
            if (e.ErrorCode != 995 && !e.Message.Contains("closed")) {
                Debug.LogError($"🔥 HttpListenerException: {e.Message}");
            }
        }
        catch (Exception e) {
            Debug.LogError($"🔥 Exception in listener thread: {e.Message}");
        }
    }

    private void ListenerCallback(IAsyncResult result) {
        if (_serverStopping)
            return;

        var listener = (HttpListener)result.AsyncState;
        HttpListenerContext context = null;

        try {
            context = listener.EndGetContext(result);
            if (context != null) {
                requestQueue.Enqueue(context);
            }
        }
        catch (Exception e) {
            Debug.LogWarning($"⚠️ ListenerCallback exception: {e.Message}");
        }
    }

    #endregion

    #region リクエスト処理

    private bool IsIpAllowed(string remoteIP) {
        IPAddress remoteAddress = IPAddress.Parse(remoteIP);
        var config = ServerConfig.Instance;

        // 許可されたIPリストが空なら localhost(127.0.0.1, ::1) のみ許可
        if (config.allowedRemoteIPs == null || config.allowedRemoteIPs.Count == 0)
            return IPAddress.IsLoopback(remoteAddress);

        // 許可IPがある場合
        foreach (var allowedIp in config.allowedRemoteIPs) {
            if (IPAddress.TryParse(allowedIp, out var allowedAddress)) {
                if (allowedAddress.Equals(remoteAddress))
                    return true;
            }
        }
        // 最後にループバックは常にOK
        return IPAddress.IsLoopback(remoteAddress);
    }

    private bool IsLocal(string remoteIP) {
        if (string.IsNullOrEmpty(remoteIP))
            return false;

        if (!IPAddress.TryParse(remoteIP, out var ipAddress)) {
            Debug.LogWarning($"⚠️ IsLocal: 無効なIPアドレス形式 → {remoteIP}");
            return false;
        }
        return IPAddress.IsLoopback(ipAddress);
    }

    private bool IsSafeUrlPath(string path, int maxLength = 256) {
        if (string.IsNullOrEmpty(path) || path.Length > maxLength)
            return false;

        foreach (char c in path) {
            if (!(('a' <= c && c <= 'z') ||
                  ('A' <= c && c <= 'Z') ||
                  ('0' <= c && c <= '9') ||
                  c == '-' || c == '_' || c == '.' || c == '/')) {
                return false;
            }
        }
        return true;
    }

    private bool CheckFileExists(string relativePath) {
        string fullPath = UserPaths.GetFullPath(relativePath);
        return File.Exists(fullPath);
    }

    private void ProcessRequestOnMainThread(HttpListenerContext context) {
        string remoteIP = context.Request.RemoteEndPoint.Address.ToString();

        Debug.Log($"🌐 Received request from {remoteIP}, URL: {context.Request.Url}, Host: {context.Request.Headers["Host"]}");

        // localhost制限が有効なら localhost(127.0.0.1 / ::1)以外は拒否
        if (listenLocalhostOnly && !IsLocal(remoteIP)) {
            Debug.LogWarning($"🚫 localhost-only モードでリモートIPからのアクセスを拒否: {remoteIP}");
            SendJsonResponse(context, 403, $"Access denied (localhost-only): {remoteIP}");
            return;
        }

        // allowedRemoteIPs チェック
        if (!IsIpAllowed(remoteIP)) {
            Debug.LogWarning($"🚫 Access denied from {remoteIP}");
            SendJsonResponse(context, 403, $"Access denied from {remoteIP}");
            return;
        }

        // Editor停止中などで Application.isPlaying==false なら503
        if (!Application.isPlaying) {
            SendJsonResponse(context, 503, "Shutdown in progress.");
            return;
        }

        // URLパスを安全にチェック
        string urlPath = context.Request.Url.AbsolutePath.ToLower().TrimEnd('/');
        if (urlPath == "")
            urlPath = "/";
        if (!IsSafeUrlPath(urlPath)) {
            Debug.LogWarning($"⚠️ Potential injection attempt in URL path from {remoteIP}: urlPath='{urlPath}'");
            SendJsonResponse(context, 400, "Invalid characters in URL path.");
            return;
        }
        // favicon.ico の処理
        if (urlPath == "/favicon.ico") {
            try {
                var asset = Resources.Load<TextAsset>("favicon.ico");
                if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                    throw new Exception("not found");

                SendResponse(context, 200, "image/x-icon", asset.bytes);
            }
            catch (Exception ex) {
                Debug.LogWarning("🔥 favicon.ico failed: " + ex.Message);
                SendPlainTextResponse(context, 500, "Internal Server Error.");
            }

            return;
        }
        // 拡張子 .png に対応（Resources から読み込み）
        if (urlPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
            string fileName = Path.GetFileName(urlPath);
            if (fileName.Contains("/") || fileName.Contains("\\")) {
                SendPlainTextResponse(context, 400, "Bad Request: Invalid file name.");
                return;
            }

            try {
                var asset = Resources.Load<TextAsset>(fileName);
                if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                    throw new Exception("not found");

                SendResponse(context, 200, "image/png", asset.bytes);
            }
            catch (Exception ex) {
                Debug.LogWarning("🔥 PNG load failed: " + ex.Message);
                SendPlainTextResponse(context, 404, "PNG file not found.");
            }
            return;
        }

        /*
        // gvah.png の処理
        if (urlPath == "/gvah.png") {
            try {
                var asset = Resources.Load<TextAsset>("gvah.png");
                if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                    throw new Exception("not found");

                SendResponse(context, 200, "image/png", asset.bytes);
            } catch (Exception ex) {
                Debug.LogError("🔥 gvah.png failed: " + ex.Message);
                SendPlainTextResponse(context, 500, "Internal Server Error.");
            }
            return;
        }
        if (urlPath == "/gvah_w.png") {
            try {
                var asset = Resources.Load<TextAsset>("gvah_w.png");
                if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                    throw new Exception("not found");

                SendResponse(context, 200, "image/png", asset.bytes);
            } catch (Exception ex) {
                Debug.LogError("🔥 gvah_w.png failed: " + ex.Message);
                SendPlainTextResponse(context, 500, "Internal Server Error.");
            }
            return;
        }
        */
        // /i18n-loader.js の処理
        if (urlPath == "/i18n-loader.js") {
            try {
                TextAsset loaderJs = Resources.Load<TextAsset>("i18n-loader");
                if (loaderJs == null || string.IsNullOrEmpty(loaderJs.text))
                    throw new Exception("i18n-loader not found.");

                SendResponse(context, 200, "text/javascript; charset=UTF-8", Encoding.UTF8.GetBytes(loaderJs.text));
            }
            catch (Exception ex) {
                Debug.LogError($"🔥 i18n-loader.js failed: {ex.Message}");
                SendPlainTextResponse(context, 404, "i18n-loader.js not found.");
            }
            return;
        }

        // /localize.json の処理
        if (urlPath == "/localize.json") {
            try {
                TextAsset locJson = Resources.Load<TextAsset>("localize");
                if (locJson == null || string.IsNullOrEmpty(locJson.text))
                    throw new Exception("localize.json not found.");

                SendResponse(context, 200, "application/json; charset=UTF-8", Encoding.UTF8.GetBytes(locJson.text));
            }
            catch (Exception ex) {
                Debug.LogError($"🔥 localize.json failed: {ex.Message}");
                SendPlainTextResponse(context, 404, "localize.json not found.");
            }
            return;
        }

        // ルートパス "/" の場合、クエリが無ければ index.html を返す
        if (urlPath == "/") {
            if (string.IsNullOrEmpty(context.Request.Url.Query)) {
                TextAsset indexHtml = Resources.Load<TextAsset>("index");
                if (indexHtml == null || string.IsNullOrEmpty(indexHtml.text)) {
                    SendPlainTextResponse(context, 500, "index.html not found or empty.");
                    return;
                }
                SendHtmlResponse(context, 200, indexHtml.text);
                return;
            }
        }

        // target パラメータチェック
        NameValueCollection query = context.Request.QueryString;
        string target = query["target"];
        if (string.IsNullOrEmpty(target)) {
            SendJsonResponse(context, 404, i18nMsg.ERROR_UNKNOWN_COMMAND);
            return;
        }

        // "multiple"連続コマンドかどうかチェック
        string rawQuery = context.Request.Url.Query.TrimStart('?');
        string[] tokens = rawQuery.Split('&');
        string firstTarget = null;
        string firstCmd = null;
        foreach (string token in tokens) {
            if (token.StartsWith("target=") && firstTarget == null)
                firstTarget = token.Substring(7);
            else if (token.StartsWith("cmd=") && firstCmd == null)
                firstCmd = token.Substring(4);
            if (firstTarget != null && firstCmd != null)
                break;
        }
        if (firstTarget == "multiple" && firstCmd == "exec_all") {
            string decodedQuery = WebUtility.UrlDecode(rawQuery);
            if (HandleMultipleCommandsDirectly(decodedQuery, out string errorMessage)) {
                SendJsonResponse(context, 200, "連続コマンド処理中");
            }
            else {
                SendJsonResponse(context, 400, errorMessage);
            }
            return;
        }

        // 通常の target=xxx を処理
        if (commandHandlers.ContainsKey(target)) {
            commandHandlers[target].HandleCommand(context, query);
        }
        else {
            SendJsonResponse(context, 404, i18nMsg.ERROR_UNKNOWN_COMMAND);
        }
    }

    [System.Serializable]
    private class CmdArrayWrapper {
        public string[] cmds;
    }

    private bool HandleMultipleCommandsDirectly(string rawQuery, out string errorMessage) {
        errorMessage = null;
        if (string.IsNullOrEmpty(rawQuery)) {
            errorMessage = "Empty query.";
            return false;
        }

        string[] parts = rawQuery.Split('&');
        var commands = new List<NameValueCollection>();
        NameValueCollection currentCommand = null;

        foreach (string part in parts) {
            // target=... があれば新コマンド開始
            if (part.StartsWith("target=", StringComparison.Ordinal)) {
                // 直前が multiple でなければ追加
                if (currentCommand != null) {
                    if (!string.Equals(currentCommand["target"], "multiple", StringComparison.OrdinalIgnoreCase))
                        commands.Add(currentCommand);
                }
                currentCommand = new NameValueCollection();
                var keyValue = part.Split(new char[] { '=' }, 2);
                if (keyValue.Length != 2 || string.IsNullOrEmpty(keyValue[1])) {
                    errorMessage = $"Invalid target parameter: {part}";
                    return false;
                }
                currentCommand.Add("target", keyValue[1]);
            }
            else if (currentCommand != null) {
                var keyValue = part.Split(new char[] { '=' }, 2);
                if (keyValue.Length != 2 || string.IsNullOrEmpty(keyValue[0])) {
                    errorMessage = $"Invalid parameter: {part}";
                    return false;
                }
                currentCommand.Add(keyValue[0], keyValue[1]);
            }
            else {
                errorMessage = $"Parameter without target encountered: {part}";
                return false;
            }
        }

        if (currentCommand != null && !string.Equals(currentCommand["target"], "multiple", StringComparison.OrdinalIgnoreCase)) {
            commands.Add(currentCommand);
        }

        // 複数実行を許可しないコマンド例
        var unsupportedCommands = new List<(string target, string cmd)>
        {
            ("vrm", "load"),
            ("animation", "play_vrma"),
            ("camera", "adjust"),
        };

        // 検証
        foreach (var cmdQuery in commands) {
            string target = cmdQuery["target"];
            string cmd = cmdQuery["cmd"];

            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(cmd)) {
                errorMessage = "Missing target or cmd in command: " +
                               string.Join("&", cmdQuery.AllKeys.Select(k => k + "=" + cmdQuery[k]));
                return false;
            }

            if (!commandHandlers.ContainsKey(target)) {
                errorMessage = $"Unknown or missing target: {target}";
                return false;
            }

            if (unsupportedCommands.Any(c =>
                string.Equals(c.target, target, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.cmd, cmd, StringComparison.OrdinalIgnoreCase))) {
                errorMessage = $"{target}={cmd} does not support multiple execute.";
                return false;
            }
        }

        // 実行 (context=null で呼ぶ)
        foreach (var cmdQuery in commands) {
            string target = cmdQuery["target"];
            commandHandlers[target].HandleCommand(null, cmdQuery);
        }
        return true;
    }

    #endregion

    #region アプリケーション終了処理

    public void InvokeShutdown() {
        StartCoroutine(ShutdownAfterDelay(0.1f));
    }

    private IEnumerator ShutdownAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion


    private void SendResponse(HttpListenerContext context, int statusCode, string contentType, byte[] content) {
        try {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = content.Length;
            context.Response.OutputStream.Write(content, 0, content.Length);
        }
        catch (Exception ex) {
            //Debug.LogError($"🔥 SendResponse failed: {ex.Message}");
            Debug.LogWarning($"⚠️ SendResponse transport failure: {ex.Message}");
        }
        finally {
            try { context.Response.OutputStream.Close(); } catch { }
        }
    }

    private void SendTextResponse(HttpListenerContext context, int statusCode, string content, string contentType) {
        byte[] buffer = Encoding.UTF8.GetBytes(content);
        SendResponse(context, statusCode, contentType, buffer);
    }

    private void SendPlainTextResponse(HttpListenerContext context, int statusCode, string message) {
        SendTextResponse(context, statusCode, message, "text/plain; charset=UTF-8");
    }

    private void SendHtmlResponse(HttpListenerContext context, int statusCode, string html) {
        SendTextResponse(context, statusCode, html, "text/html; charset=UTF-8");
    }

    private void SendJsonResponse(HttpListenerContext context, int statusCode, string message) {
        var responseObj = new ServerResponse(statusCode, statusCode == 200, message);
        SendTextResponse(context, statusCode, responseObj.ToJson(), "application/json; charset=UTF-8");
    }

}