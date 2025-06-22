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

    // Wave playback functionality
    private AudioSource audioSource;
    private Coroutine playbackRoutine;
    private string currentAudioId;
    private float playStartTime;
    private AudioLipSync lipSync;
    private readonly System.Collections.Generic.Queue<WaveItem> waveQueue = new System.Collections.Generic.Queue<WaveItem>();
    private string lastConcurrency;

    private class WaveItem {
        public byte[] data;
        public HttpListenerContext context;
        public string id;
        public float volume;
        public bool? spatial;
    }

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
        // Play mode でのみ設定をロード
        if (Application.isPlaying) {
            LoadConfig();  // ServerConfig.Instance から設定をロード
            i18nMsg.InitializeLocalization();
        }
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
        this.lipSync = UnityEngine.Object.FindAnyObjectByType<AudioLipSync>();
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
        if (config == null) {
            Debug.LogWarning("[AnimationServer] ServerConfig.Instance is null, using default values");
            return;
        }
        
        httpPort = config.httpPort != 0 ? config.httpPort : 34560;
        httpsPort = config.httpsPort != 0 ? config.httpsPort : 34561;
        useHttp = config.useHttp;
        useHttps = config.useHttps;
        listenLocalhostOnly = config.listenLocalhostOnly;
        allowedRemoteIPs = config.allowedRemoteIPs ?? new List<string>();
        autoPrepareSeamless = config.autoPrepareSeamless;
        vsync = config.vsync;
        targetFramerate = config.targetFramerate;

        Debug.Log($"[DEBUG] listenLocalhostOnly = {config.listenLocalhostOnly}");
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
        
        // configがnullの場合はlocalhostのみ許可
        if (config == null) {
            return IPAddress.IsLoopback(remoteAddress);
        }

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

        // Wave playback endpoint
        if (urlPath == "/waveplay" || urlPath.StartsWith("/waveplay/")) {
            var config = ServerConfig.Instance;
            if (config != null && config.wavePlaybackEnabled) {
                HandleWavePlaybackRequest(context);
            } else {
                SendJsonResponse(context, 503, "Wave playback is disabled");
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

    #region Wave Playback Handling

    private void HandleWavePlaybackRequest(HttpListenerContext context) {
        if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/waveplay/ping") {
            SendWaveJsonResponse(context, 200, "ok");
            return;
        }
        
        if (context.Request.HttpMethod != "POST") {
            SendWaveJsonResponse(context, 405, "method_not_allowed");
            return;
        }

        string path = context.Request.Url.AbsolutePath;
        if (!(string.IsNullOrEmpty(path) || path == "/waveplay" || path == "/waveplay/")) {
            SendWaveJsonResponse(context, 404, "not_found");
            return;
        }

        if (!context.Request.ContentType?.StartsWith("audio/wav") ?? true) {
            SendWaveJsonResponse(context, 415, "unsupported_media_type");
            return;
        }

        long len = context.Request.ContentLength64;
        var config = ServerConfig.Instance;
        int max = config?.wavePayloadMaxBytes ?? 5000000;
        if (len <= 0 || len > max) {
            SendWaveJsonResponse(context, 413, "payload_too_large");
            return;
        }

        string audioId = context.Request.Headers["X-Audio-ID"];
        if (string.IsNullOrEmpty(audioId)) audioId = Guid.NewGuid().ToString();

        Debug.Log($"[Wave] playback request id={audioId} length={len}");

        float volume = 1.0f;
        string volHeader = context.Request.Headers["X-Volume"];
        if (!string.IsNullOrEmpty(volHeader) && float.TryParse(volHeader, out float vol)) {
            volume = Mathf.Clamp(vol, 0f, 2f);
        }

        bool? spatialOverride = null;
        string spatialHeader = context.Request.Headers["X-Spatial"];
        if (!string.IsNullOrEmpty(spatialHeader)) {
            spatialOverride = spatialHeader.ToLower() == "y" || spatialHeader.ToLower() == "yes";
        }

        byte[] data;
        using (var ms = new MemoryStream()) {
            context.Request.InputStream.CopyTo(ms);
            data = ms.ToArray();
        }

        EnqueueWave(data, context, audioId, volume, spatialOverride);
    }

    private void EnqueueWave(byte[] data, HttpListenerContext context, string audioId, float headerVolume, bool? spatialOverride) {
        var config = ServerConfig.Instance;
        string mode = config?.wavePlaybackConcurrency ?? "interrupt";
        if (lastConcurrency != mode) {
            if (lastConcurrency == "queue" && mode != "queue") waveQueue.Clear();
            lastConcurrency = mode;
        }

        var item = new WaveItem{ data = data, context = context, id = audioId, volume = headerVolume, spatial = spatialOverride };

        if (mode == "queue") {
            if (audioSource != null && audioSource.isPlaying) {
                waveQueue.Enqueue(item);
                SendWaveJsonResponse(context, 200, "queued", audioId);
                return;
            }
            PlayWave(item);
            return;
        }
        if (mode == "reject" && audioSource != null && audioSource.isPlaying) {
            SendWaveJsonResponse(context, 409, "busy");
            return;
        }
        PlayWave(item);
    }

    private void PlayWave(WaveItem item) {
        var data = item.data;
        var context = item.context;
        string audioId = item.id;
        float headerVolume = item.volume;
        bool? spatialOverride = item.spatial;
        
        if (audioSource == null) {
            var go = new GameObject("WavePlaybackSource");
            audioSource = go.AddComponent<AudioSource>();
        }
        if (lipSync == null) lipSync = GameObject.FindObjectOfType<AudioLipSync>();
        
        try {
            if (!TryParseWav(data, out float[] samples, out int sampleRate)) {
                SendWaveJsonResponse(context, 422, "invalid_wav");
                return;
            }
            var clip = AudioClip.Create("wave", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            var config = ServerConfig.Instance;
            bool spatial = spatialOverride ?? (config?.waveSpatializationEnabled ?? true);
            audioSource.spatialBlend = spatial ? 1f : 0f;
            float baseVol = config?.wavePlaybackVolume ?? 1.0f;
            string prevId = null;
            if (audioSource.isPlaying) {
                prevId = currentAudioId;
                audioSource.Stop();
                if (playbackRoutine != null) StopCoroutine(playbackRoutine);
                Telemetry.LogEvent("wave_interrupt", new System.Collections.Generic.Dictionary<string, object>{{"old_id", prevId},{"new_id", audioId}});
            }
            audioSource.volume = baseVol * headerVolume;
            audioSource.clip = clip;
            audioSource.Play();
            currentAudioId = audioId;
            playStartTime = Time.time;
            playbackRoutine = StartCoroutine(PlaybackCoroutine(samples, sampleRate, audioId));
            Telemetry.LogEvent("wave_start", new System.Collections.Generic.Dictionary<string, object>{{"id", audioId},{"bytes", data.Length},{"ip", context.Request.RemoteEndPoint.Address.ToString()}});
            if (prevId != null)
                SendWaveJsonResponse(context, 200, "interrupted", audioId, prevId);
            else
                SendWaveJsonResponse(context, 200, "ok", audioId);
        } catch (Exception e) {
            Debug.LogError($"[Wave] playback error: {e.Message}");
            SendWaveJsonResponse(context, 500, "internal_error");
        }
    }

    private IEnumerator PlaybackCoroutine(float[] samples, int sampleRate, string audioId) {
        int step = Mathf.Max(1, sampleRate / 100); // 10ms
        var config = ServerConfig.Instance;
        float offset = (config?.lipSyncOffsetMs ?? 0) / 1000f;
        float nextTime = Time.time + offset;
        for (int i = 0; i < samples.Length; i += step) {
            float sum = 0f;
            int count = 0;
            for (int j = 0; j < step && i + j < samples.Length; j++) { sum += samples[i + j] * samples[i + j]; count++; }
            float rms = Mathf.Sqrt(sum / count);
            lipSync?.FeedWaveRms(rms);
            float delay = nextTime - Time.time;
            if (delay > 0f) yield return new WaitForSeconds(delay); else yield return null;
            nextTime += 0.01f;
        }
        lipSync?.FeedWaveRms(0f);
        Telemetry.LogEvent("wave_complete", new System.Collections.Generic.Dictionary<string, object>{{"id", audioId},{"duration_ms", (int)((Time.time - playStartTime)*1000)}});
        currentAudioId = null;
        playbackRoutine = null;
        if ((config?.wavePlaybackConcurrency ?? "interrupt") == "queue" && waveQueue.Count > 0) {
            var nextItem = waveQueue.Dequeue();
            PlayWave(nextItem);
        }
    }

    private bool TryParseWav(byte[] bytes, out float[] samples, out int sampleRate) {
        samples = null;
        sampleRate = 48000;
        if (bytes.Length < 44) return false;
        using (var ms = new MemoryStream(bytes)) using (var br = new BinaryReader(ms)) {
            string riff = new string(br.ReadChars(4));
            if (riff != "RIFF") return false;
            br.ReadInt32();
            string wave = new string(br.ReadChars(4));
            if (wave != "WAVE") return false;
            string fmt = new string(br.ReadChars(4));
            if (fmt != "fmt ") return false;
            int fmtSize = br.ReadInt32();
            ushort audioFormat = br.ReadUInt16();
            ushort channels = br.ReadUInt16();
            sampleRate = br.ReadInt32();
            br.ReadInt32(); // byte rate
            br.ReadUInt16(); // block align
            ushort bitsPerSample = br.ReadUInt16();
            ms.Position += fmtSize - 16;
            string dataId = new string(br.ReadChars(4));
            while (dataId != "data") {
                int skip = br.ReadInt32();
                ms.Position += skip;
                if (ms.Position + 4 > ms.Length) return false;
                dataId = new string(br.ReadChars(4));
            }
            int dataSize = br.ReadInt32();
            if (audioFormat != 1 || bitsPerSample != 16 || channels != 1) return false;
            short[] pcm = new short[dataSize / 2];
            for (int i = 0; i < pcm.Length; i++) pcm[i] = br.ReadInt16();
            samples = new float[pcm.Length];
            for (int i = 0; i < pcm.Length; i++) samples[i] = pcm[i] / 32768f;
        }
        return true;
    }

    private void SendWaveJsonResponse(HttpListenerContext ctx, int status, string msg, string id = null, string prevId = null) {
        string body;
        if (prevId != null)
            body = $"{{\"status\":\"{msg}\",\"prev_id\":\"{prevId}\",\"id\":\"{id}\"}}";
        else if (id != null)
            body = $"{{\"status\":\"{msg}\",\"id\":\"{id}\"}}";
        else
            body = $"{{\"status\":\"{msg}\"}}";
        var buf = System.Text.Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = buf.Length;
        try {
            using (var s = ctx.Response.OutputStream) {
                s.Write(buf, 0, buf.Length);
            }
        } catch (Exception ex) {
            Debug.LogWarning($"⚠️ SendWaveJsonResponse transport failure: {ex.Message}");
        }
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
