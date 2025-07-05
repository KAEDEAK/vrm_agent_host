using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Reflection;

public enum AnimationType {
    IntBased,
    PlayBased
}

[Serializable]
public struct AnimationMappingEntry {
    public string logicalKey;
    public string alias;
    public AnimationType type;
    public int intValue;
    public string stateName;
    public int playLayer;
    public float normalizedTime;
}

[Serializable]
public class ServerConfig {

    // --- Singleton ---
    private static ServerConfig _instance;
    private static readonly object _lock = new object();
    private static readonly string configPath = UserPaths.ConfigPath;
    public static ServerConfig Instance {
        get {
#if UNITY_EDITOR
            // Editorモードでは初期化しない（厳密チェック）
            if (!UnityEditor.EditorApplication.isPlaying) {
                DebugLogger.LogVerbose("[ServerConfig] Access denied: Editor is not in play mode");
                return null;
            }
#endif
            // Play mode でのみ初期化（二重チェック）
            if (!Application.isPlaying) {
                DebugLogger.LogVerbose("[ServerConfig] Access denied: Application.isPlaying is false");
                return null;
            }
            
            if (_instance == null) {
                lock (_lock) {
                    if (_instance == null) {
                        // 最終チェック：初期化時にも再度確認
#if UNITY_EDITOR
                        if (!UnityEditor.EditorApplication.isPlaying) {
                            Debug.LogWarning("[ServerConfig] Initialization blocked: Editor not in play mode");
                            return null;
                        }
#endif
                        if (!Application.isPlaying) {
                            Debug.LogWarning("[ServerConfig] Initialization blocked: Application not playing");
                            return null;
                        }
                        
                        _instance = LoadConfig();
                        if (_instance != null) {
                            Debug.Log("[ServerConfig] Successfully initialized in runtime mode");
                        }
                    }
                }
            }
            return _instance;
        }
    }

    private static bool EnsureConfigFile() {
        if (File.Exists(configPath)) return true;

        try {
            // ディレクトリが無ければ作成
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));

            // Resources/default_config.json を読み込み
            TextAsset defaultJson = Resources.Load<TextAsset>("default_config");
            if (defaultJson == null) {
                // 本来は default_config.json を Resources フォルダ直下に配置しておく必要があります。
                Debug.LogError("[ServerConfig] \"Resources/default_config.json\" が見つかりません！初回起動時のデフォルト設定を生成できません。");
                // 最低限、空の Object ではなく、最低限の必須フィールドだけでも書き出したい場合はここを適宜編集してください。
                File.WriteAllText(configPath, "{}", Encoding.UTF8);
                return true;
            }

            // デフォルト定義をそのまま書き出し → コメント用フィールド（something_note など）も一切カットせずに丸ごと保持する
            File.WriteAllText(configPath, defaultJson.text, Encoding.UTF8);
            Debug.Log($"[ServerConfig] 初回起動時 default_config.json の内容をそのまま config.json に保存しました → path={configPath}");
            return true;
        }
        catch (Exception ex) {
            Debug.LogError($"[ServerConfig] config.json 生成失敗: {ex.Message}");
            return false;
        }
    }


    // --- 設定フィールド ---
    public int httpPort = 34560;
    public int httpsPort = 34561;
    public bool useHttp = true;
    public bool useHttps = false;
    public bool listenLocalhostOnly = true;
    public List<string> allowedRemoteIPs = new List<string> { "127.0.0.1", "::1" };
    public bool autoPrepareSeamless = false;
    public bool vsync = true;
    public int targetFramerate = 60;
    // --- Wave Playback ---
    public bool wavePlaybackEnabled = false;
    public float wavePlaybackVolume = 1.0f;
    public bool waveSpatializationEnabled = true;
    public int  wavePayloadMaxBytes = 5000000;
    public bool waveListenerAutoRestart = true;
    public int  lipSyncOffsetMs = 0;
    public string wavePlaybackConcurrency = "interrupt";
    public List<FileControlEntry> fileControl = new List<FileControlEntry>();

    [SerializeField] private CameraConfigData camera = new CameraConfigData();
    public CameraConfigData Camera => camera;
    [SerializeField] private WindowConfigData window = new WindowConfigData();
    public WindowConfigData Window => window;
    public LipSyncConfig lipSync = new LipSyncConfig();
    public LoggingConfig logging = new LoggingConfig();
    public List<string> outputFilters = new List<string>();

    // --- アニメーションマッピング ---
    private static Dictionary<string, Dictionary<string, AnimationMappingEntry>> animationMappings;

    // Idle: :contentReference[oaicite:0]{index=0}&#8203;:contentReference[oaicite:1]{index=1} :contentReference[oaicite:2]{index=2}&#8203;:contentReference[oaicite:3]{index=3}  
    // Other: :contentReference[oaicite:4]{index=4}&#8203;:contentReference[oaicite:5]{index=5}  
    // Layer: :contentReference[oaicite:6]{index=6}&#8203;:contentReference[oaicite:7]{index=7} :contentReference[oaicite:8]{index=8}&#8203;:contentReference[oaicite:9]{index=9}  
    private static readonly Dictionary<string, Dictionary<string, AnimationMappingEntry>> defaultMappings =
        new Dictionary<string, Dictionary<string, AnimationMappingEntry>>
    {
        { "Idle", new Dictionary<string, AnimationMappingEntry> {
            { "generic",     new AnimationMappingEntry{ logicalKey="generic", alias="Generic_01",       type=AnimationType.IntBased,  intValue=1 } },
            { "angry",       new AnimationMappingEntry{ logicalKey="angry",   alias="Angry_01",         type=AnimationType.IntBased,  intValue=2 } },
            { "brave",       new AnimationMappingEntry{ logicalKey="brave",   alias="Brave_01",         type=AnimationType.IntBased,  intValue=3 } },
            { "calm",        new AnimationMappingEntry{ logicalKey="calm",    alias="Calm_01",          type=AnimationType.IntBased,  intValue=4 } },
            { "calm_02",     new AnimationMappingEntry{ logicalKey="calm_02", alias="Calm_02",          type=AnimationType.IntBased,  intValue=5 } },
            { "concern",     new AnimationMappingEntry{ logicalKey="concern", alias="Concern_01",       type=AnimationType.IntBased,  intValue=6 } },
            { "classy",      new AnimationMappingEntry{ logicalKey="classy",  alias="Classy_01",        type=AnimationType.IntBased,  intValue=7 } },
            { "cute",        new AnimationMappingEntry{ logicalKey="cute",    alias="Cute_01",          type=AnimationType.IntBased,  intValue=8 } },
            { "denying",     new AnimationMappingEntry{ logicalKey="denying", alias="Deny_01",          type=AnimationType.IntBased,  intValue=9 } },
            { "energetic",   new AnimationMappingEntry{ logicalKey="energetic",alias="Energetic_01",     type=AnimationType.IntBased,  intValue=10 } },
            { "energetic_02",new AnimationMappingEntry{ logicalKey="energetic_02",alias="Energetic_02", type=AnimationType.IntBased,  intValue=11 } },
            { "sexy",        new AnimationMappingEntry{ logicalKey="sexy",    alias="Sexy_01",          type=AnimationType.IntBased,  intValue=12 } },
            { "pitiable",    new AnimationMappingEntry{ logicalKey="pitiable",alias="Pitiable_01",      type=AnimationType.IntBased,  intValue=13 } },
            { "stressed",    new AnimationMappingEntry{ logicalKey="stressed",alias="Stress_01",        type=AnimationType.IntBased,  intValue=14 } },
            { "surprise",    new AnimationMappingEntry{ logicalKey="surprise",alias="Surprise_01",      type=AnimationType.IntBased,  intValue=15 } },
            { "think",       new AnimationMappingEntry{ logicalKey="think",   alias="Think_01",         type=AnimationType.IntBased,  intValue=16 } },
            { "what",        new AnimationMappingEntry{ logicalKey="what",    alias="What_01",          type=AnimationType.IntBased,  intValue=17 } },
            { "boyish",      new AnimationMappingEntry{ logicalKey="boyish",  alias="Boyish_01",        type=AnimationType.IntBased,  intValue=18 } },
            { "cry",         new AnimationMappingEntry{ logicalKey="cry",     alias="Cry_01",           type=AnimationType.IntBased,  intValue=19 } },
            { "laugh",       new AnimationMappingEntry{ logicalKey="laugh",   alias="Laugh_01",         type=AnimationType.IntBased,  intValue=20 } },
            { "cute_02",     new AnimationMappingEntry{ logicalKey="cute_02", alias="Cute_02",          type=AnimationType.IntBased,  intValue=21 } },
            { "angry_02",    new AnimationMappingEntry{ logicalKey="angry_02",alias="Angry_02",         type=AnimationType.IntBased,  intValue=22 } },
            { "fedup",       new AnimationMappingEntry{ logicalKey="fedup",   alias="Fedup_01",         type=AnimationType.IntBased,  intValue=23 } },
            { "fedup_02",    new AnimationMappingEntry{ logicalKey="fedup_02",alias="Fedup_02",         type=AnimationType.IntBased,  intValue=24 } },
            { "cute_03",     new AnimationMappingEntry{ logicalKey="cute_03", alias="Cute_03",          type=AnimationType.IntBased,  intValue=25 } },
            { "cat",         new AnimationMappingEntry{ logicalKey="cat",     alias="Cat_01",           type=AnimationType.IntBased,  intValue=26 } },
            { "pointfinger", new AnimationMappingEntry{ logicalKey="pointfinger",alias="PointFinger_01",type=AnimationType.IntBased,  intValue=27 } },
            { "energetic_03",new AnimationMappingEntry{ logicalKey="energetic_03",alias="Energetic_03",type=AnimationType.IntBased,  intValue=28 } },
            { "sexy_02",     new AnimationMappingEntry{ logicalKey="sexy_02", alias="Sexy_02",          type=AnimationType.IntBased,  intValue=29 } },
            { "sexy_03",     new AnimationMappingEntry{ logicalKey="sexy_03", alias="Sexy_03",          type=AnimationType.IntBased,  intValue=30 } },
        }},
        { "Other", new Dictionary<string, AnimationMappingEntry> {
            { "walk",         new AnimationMappingEntry{ logicalKey="walk",        alias="Walk_01",      type=AnimationType.IntBased,  intValue=1 } },
            { "run",          new AnimationMappingEntry{ logicalKey="run",         alias="Run_01",       type=AnimationType.IntBased,  intValue=2 } },
            { "wave_hand",    new AnimationMappingEntry{ logicalKey="wave_hand",   alias="WaveHand_01",  type=AnimationType.IntBased,  intValue=3 } },
            { "wave_hands",   new AnimationMappingEntry{ logicalKey="wave_hands",  alias="WaveHands_01", type=AnimationType.IntBased,  intValue=4 } },
            { "wave_arm",     new AnimationMappingEntry{ logicalKey="wave_arm",    alias="WaveArm_01",   type=AnimationType.IntBased,  intValue=5 } },
            { "what",         new AnimationMappingEntry{ logicalKey="what",        alias="What_01",      type=AnimationType.IntBased,  intValue=6 } },
            { "energetic",    new AnimationMappingEntry{ logicalKey="energetic",   alias="Energetic_01", type=AnimationType.IntBased,  intValue=7 } },
            { "cute",         new AnimationMappingEntry{ logicalKey="cute",        alias="Cute_01",      type=AnimationType.IntBased,  intValue=8 } },
            { "cat",          new AnimationMappingEntry{ logicalKey="cat",         alias="Cat_01",       type=AnimationType.IntBased,  intValue=9 } },
            { "point_finger", new AnimationMappingEntry{ logicalKey="point_finger",alias="PointFinger_01",type=AnimationType.IntBased,  intValue=10 } },
        }},
        { "Layer", new Dictionary<string, AnimationMappingEntry> {
            { "start",          new AnimationMappingEntry{ logicalKey="start",          alias="Reset",             type=AnimationType.PlayBased, stateName="Layer_start",           playLayer=1, normalizedTime=0.0f } },
            { "look_away",      new AnimationMappingEntry{ logicalKey="look_away",      alias="LookAway_01",       type=AnimationType.PlayBased, stateName="Layer_look_away_01",     playLayer=1, normalizedTime=0.0f } },
            { "look_away_angry",new AnimationMappingEntry{ logicalKey="look_away_angry",alias="LookAwayAngry_01",   type=AnimationType.PlayBased, stateName="Layer_look_away_angry_01",playLayer=1, normalizedTime=0.0f } },
            { "nod_once",       new AnimationMappingEntry{ logicalKey="nod_once",       alias="NodOnce_01",        type=AnimationType.PlayBased, stateName="Layer_nod_once_01",      playLayer=1, normalizedTime=0.0f } },
            { "nod_twice",      new AnimationMappingEntry{ logicalKey="nod_twice",      alias="NodTwice_01",       type=AnimationType.PlayBased, stateName="Layer_nod_twice_01",     playLayer=1, normalizedTime=0.0f } },
            { "shake_head",     new AnimationMappingEntry{ logicalKey="shake_head",     alias="ShakeHead_01",      type=AnimationType.PlayBased, stateName="Layer_shake_head_01",    playLayer=1, normalizedTime=0.0f } },
            { "swing_body",     new AnimationMappingEntry{ logicalKey="swing_body",     alias="SwingBody_01",      type=AnimationType.PlayBased, stateName="Layer_swing_body_01",    playLayer=1, normalizedTime=0.0f } },
            { "laugh_up",       new AnimationMappingEntry{ logicalKey="laugh_up",       alias="LaughUp_01",        type=AnimationType.PlayBased, stateName="Layer_laugh_up_01",      playLayer=1, normalizedTime=0.0f } },
            { "laugh_down",     new AnimationMappingEntry{ logicalKey="laugh_down",     alias="LaughDown_01",      type=AnimationType.PlayBased, stateName="Layer_laugh_down_01",    playLayer=1, normalizedTime=0.0f } },
            { "shake_body",     new AnimationMappingEntry{ logicalKey="shake_body",     alias="ShakeBody_01",      type=AnimationType.PlayBased, stateName="Layer_shake_body_01",    playLayer=1, normalizedTime=0.0f } },
            { "surprise",       new AnimationMappingEntry{ logicalKey="surprise",       alias="Surprise_01",       type=AnimationType.PlayBased, stateName="Layer_surprise_01",      playLayer=1, normalizedTime=0.0f } },
            { "tilt_neck",      new AnimationMappingEntry{ logicalKey="tilt_neck",      alias="TiltNeck_01",       type=AnimationType.PlayBased, stateName="Layer_tilt_neck_01",     playLayer=1, normalizedTime=0.0f } },
            { "turn_right",     new AnimationMappingEntry{ logicalKey="turn_right",     alias="TurnRight_01",      type=AnimationType.PlayBased, stateName="Layer_turn_right_01",    playLayer=1, normalizedTime=0.0f } },
            { "turn_left",      new AnimationMappingEntry{ logicalKey="turn_left",      alias="TurnLeft_01",       type=AnimationType.PlayBased, stateName="Layer_turn_left_01",     playLayer=1, normalizedTime=0.0f } },
        }},
    };

    // --- コンストラクタ（マッピング＋ロード） ---
    private ServerConfig() {
        LoadAnimationMappings();
        LoadConfigForInstance();
    }

    private static ServerConfig LoadConfig() {
        // 追加の安全チェック：LoadConfig時にも環境を確認
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying) {
            Debug.LogError("[ServerConfig] LoadConfig called in Editor non-play mode - aborting");
            return null;
        }
#endif
        if (!Application.isPlaying) {
            Debug.LogError("[ServerConfig] LoadConfig called when Application.isPlaying is false - aborting");
            return null;
        }

        EnsureConfigFile();

        try { 
            var config = new ServerConfig();
            Debug.Log("[ServerConfig] Configuration loaded successfully");
            return config;
        }
        catch (Exception ex) {
            Debug.LogError($"[ServerConfig] Failed to load configuration: {ex.Message}");
            return new ServerConfig();
        }
    }

    private void LoadAnimationMappings() {
        animationMappings = new Dictionary<string, Dictionary<string, AnimationMappingEntry>>();
        foreach (var cat in defaultMappings)
            animationMappings[cat.Key] = new Dictionary<string, AnimationMappingEntry>(cat.Value);

        if (File.Exists(configPath)) {
            var json = File.ReadAllText(configPath, System.Text.Encoding.UTF8);
            var data = JsonUtility.FromJson<ServerConfigData>(json);
            if (data != null && data.animations != null) {
                foreach (var ov in data.animations) {
                    if (!animationMappings.ContainsKey(ov.category))
                        animationMappings[ov.category] = new Dictionary<string, AnimationMappingEntry>();
                    animationMappings[ov.category][ov.key] = new AnimationMappingEntry {
                        logicalKey = ov.key,
                        alias = ov.key,
                        type = AnimationType.IntBased,
                        intValue = ov.value
                    };
                }
            }
        }
    }

    private void LoadConfigForInstance() {
        if (!File.Exists(configPath)) {                // ★ ガード復活
            Debug.LogError("[ServerConfig] config.json が無いのでデフォルト値で続行するっす");
            return;
        }

        try {
            var json = File.ReadAllText(configPath, Encoding.UTF8);
            var data = JsonUtility.FromJson<ServerConfigData>(json);
            if (data == null) {
                Debug.LogError("[ServerConfig] config.json パース失敗。デフォルト値を使用するよ～");
                return;
            }

            httpPort = data.httpPort;
            httpsPort = data.httpsPort;
            useHttp = data.useHttp;
            useHttps = data.useHttps;
            listenLocalhostOnly = data.listenLocalhostOnly;
            allowedRemoteIPs = data.allowedRemoteIPs ?? allowedRemoteIPs;
            autoPrepareSeamless = data.autoPrepareSeamless;
            vsync = data.vsync;
            targetFramerate = data.targetFramerate;
            wavePlaybackEnabled = data.wavePlaybackEnabled;
            wavePlaybackVolume = data.wavePlaybackVolume;
            waveSpatializationEnabled = data.waveSpatializationEnabled;
            wavePayloadMaxBytes = data.wavePayloadMaxBytes;
            waveListenerAutoRestart = data.waveListenerAutoRestart;
            lipSyncOffsetMs = data.lipSyncOffsetMs;
            wavePlaybackConcurrency = data.wavePlaybackConcurrency ?? "interrupt";
            fileControl = data.fileControl;
            window = data.window;
            camera = data.camera;
            lipSync = data.lipSync;
            logging = data.logging ?? new LoggingConfig();
            outputFilters = data.outputFilters;
        }
        catch (Exception ex) {
            Debug.LogError($"[ServerConfig] config.json 読み込みで例外: {ex.Message}");
        }
    }

    public void ReloadConfig() {
        LoadConfigForInstance();
        // Refresh DebugLogger settings after config reload
        DebugLogger.RefreshSettings();
    }




    public void SetAutoPrepareSeamless(bool enable) {
        autoPrepareSeamless = enable;
        Debug.Log($"autoPrepareSeamless set to: {enable}");
    }
    public bool GetAutoPrepareSeamless() => autoPrepareSeamless;

    public int GetAnimationId(string category, string animationName) {
        if (animationMappings.TryGetValue(category, out var dict) &&
            dict.TryGetValue(animationName, out var e))
            return e.intValue;
        return -1;
    }
    public string GetAnimationName(string category, int animationID) {
        if (animationMappings.TryGetValue(category, out var dict))
            foreach (var kv in dict)
                if (kv.Value.intValue == animationID)
                    return kv.Key;
        return null;
    }
    public AnimationMappingEntry? GetMappingByAlias(string category, string id) {
        if (!animationMappings.TryGetValue(category, out var dict)) return null;
        id = id.ToLowerInvariant();
        foreach (var e in dict.Values)
            if (e.logicalKey.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                e.alias.Equals(id, StringComparison.OrdinalIgnoreCase))
                return e;
        return null;
    }
    public List<string> GetLogicalKeys() {
        var list = new List<string>();
        foreach (var cat in animationMappings)
            foreach (var e in cat.Value.Values)
                list.Add($"{cat.Key}_{e.logicalKey}");
        return list;
    }
    public List<string> GetDefaultMappingKeys() {
        var list = new List<string>();
        foreach (var cat in defaultMappings)
            foreach (var e in cat.Value.Values)
                list.Add($"{cat.Key}_{e.alias}");
        return list;
    }
    /// <summary>
    /// default_config.json や既存の config.json をもとに
    /// in-memory の現在フィールド値をマージして保存する。
    /// 初回は default_config.json からマージし、2回目以降は既存の config.json を使用。
    /// </summary>
        public void SaveConfigPreserveUnknown() {
        // 1) config.json のパスを決定（例：persistentDataPath 配下）
        // string configPath = Path.Combine(Application.persistentDataPath, "config.json");
        string configPath= UserPaths.ConfigPath;

        // 2) baseTree を読み込む（2回目以降は既存 config.json、初回は default_config.json）
        object baseTree = null;

        if (File.Exists(configPath)) {
            // 2回目以降 → 既存の config.json
            string existingJson = File.ReadAllText(configPath, Encoding.UTF8);
            baseTree = ConfigJsonTree.Parse(existingJson);
        }
        else {
            // 初回 → Resources フォルダ内の default_config.json を読み込み
            TextAsset ta = Resources.Load<TextAsset>("default_config");
            if (ta == null || string.IsNullOrEmpty(ta.text)) {
                Debug.LogError("[ServerConfig] default_config.json が Resources 派に見つからないっす！");
                return;
            }
            string defaultJson = ta.text;
            baseTree = ConfigJsonTree.Parse(defaultJson);
        }

        // ───────────────────────────────────────────────────────────────────
        // 3) インスタンス(this)の全フィールドを JSON 化 → ConfigJsonTree にパース
        //    ▶ こうすると public フィールドも [Serializable] クラスも一気に辞書化できるっす！
        string instanceJson = JsonUtility.ToJson(this);                          // ①
        object srcTree = ConfigJsonTree.Parse(instanceJson);                     // ②
        // ───────────────────────────────────────────────────────────────────

        // 4) deep マージ実行
        ConfigJsonTree.MergeTo(srcTree, ref baseTree);

        // 5) マージ後のオブジェクトツリーを整形 JSON にして書き出し
        string outJson = ConfigJsonTree.Dump(baseTree, indent: 2);
        try {
            File.WriteAllText(configPath, outJson, new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex) {
            Debug.LogError($"[ServerConfig] config.json 書き込みエラー: {ex.Message}");
        }
    }


    //――――――――――――――――――――――――――――――――――――
    // 以下は既存実装のまま移植／補完
    private static string BuildJsonArray(List<string> list) {
        var sb = new StringBuilder("[");
        for (int i = 0; i < list.Count; i++) {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(Escape(list[i])).Append('"');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ReplaceOrAppend(string src, string key, string newJsonValue) {
        // 元の実装そのまま
        string pattern = $"(\"{Regex.Escape(key)}\"\\s*:\\s*)([^\\r\\n,{{}}\\[\\]]+|\"(?:[^\"\\\\]|\\\\.)*\"|\\[.*?\\]|\\{{.*?\\}})";
        var regex = new Regex(pattern, RegexOptions.Singleline);

        if (regex.IsMatch(src)) {
            return regex.Replace(src, $"\"{key}\": {newJsonValue}", 1);
        }
        else {
            int idx = src.LastIndexOf('}');
            if (idx == -1) return src;
            string insert = (src.Contains("\n") ? "\n    " : " ") + $"\"{key}\": {newJsonValue},";
            return src.Insert(idx, insert);
        }
    }

    [Serializable] private class Wrapper<T> { public T Items; public Wrapper(T items) { Items = items; } }
}

public static class StringExtensions {
    /// <summary>
    /// 文字列中の最初の <paramref name="search"/> だけを <paramref name="replace"/> に置換する
    /// </summary>
    public static string ReplaceFirst(this string source, string search, string replace) {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search)) return source;
        int pos = source.IndexOf(search, StringComparison.Ordinal);
        return pos < 0 ? source
                       : source.Substring(0, pos) + replace + source.Substring(pos + search.Length);
    }
}
