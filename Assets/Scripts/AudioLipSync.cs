using UnityEngine;
using System.Collections;
using System.Collections.Specialized;
using System.Net;
using UniVRM10;
using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.DSP;
using System;
using System.Collections.Generic;

public class AudioLipSync : MonoBehaviour {
    private const int WAVEPLAYBACK_CHANNEL_ID = 0;
    private const int WASAPI_CHANNEL_ID = 1;
    private const int MICROPHONE_CHANNEL_ID = 2;
    private enum LipSyncSource { None, Microphone, WavePlayback, WASAPI }
    private LipSyncSource currentSource = LipSyncSource.None;

    // LipSync Blend Mode System
    public enum LipSyncBlendMode {
        LIPSYNC_MODE_BLEND_EXPR,    // 現在の実装モード（表情との加算）
        LIPSYNC_MODE_RESET_EXPR,    // 一定入力後に表情をリセット
        LIPSYNC_MODE_NONE,          // 表情がある時はLipSyncを無効化
        LIPSYNC_MODE_FULL,          // 破綻覚悟で単純加算（性能比較用）
        LIPSYNC_MODE_AUTO           // BLEND_EXPRとRESET_EXPRをランダム適用
    }

    private LipSyncBlendMode currentBlendMode = LipSyncBlendMode.LIPSYNC_MODE_AUTO;
    
    // RESET_EXPR モード用の変数
    private float lipSyncInputThreshold = 0.3f;     // リセットトリガーとなる入力レベル
    private float lipSyncInputDuration = 0f;        // 連続入力時間
    private float lipSyncInputRequiredTime = 1.0f;  // リセットまでの必要時間
    private float lipSyncResetDelay = 1.0f;         // リセット遅延時間
    private bool isWaitingForReset = false;         // リセット待機中フラグ
    private Coroutine resetExpressionCoroutine;     // リセット用コルーチン
    
    // AUTO モード用の変数
    private float autoModeChangeInterval = 5.0f;    // モード切り替え間隔（秒）
    private float lastAutoModeChange = 0f;          // 最後のモード切り替え時間
    private LipSyncBlendMode currentAutoMode = LipSyncBlendMode.LIPSYNC_MODE_BLEND_EXPR;
    
    // 表情検出用
    private readonly ExpressionKey[] emotionExpressionKeys = {
        ExpressionKey.CreateFromPreset(ExpressionPreset.angry),
        ExpressionKey.CreateFromPreset(ExpressionPreset.happy),
        ExpressionKey.CreateFromPreset(ExpressionPreset.sad),
        ExpressionKey.CreateFromPreset(ExpressionPreset.surprised),
        ExpressionKey.CreateFromPreset(ExpressionPreset.relaxed)
    };

    private AudioClip microphoneClip;
    private string microphoneDevice;
    private WasapiLoopbackCapture wasapiCapture;
    private float wasapiVolume = 0f;
    private object wasapiLock = new object();

    private const int sampleWindow = 128;
    private float[] samples = new float[sampleWindow];
    private float lipSyncSmoothing = 0.1f;
    private float lastVolume = 0f;
    private bool isLipSyncActive = false;

    private float scaleMultiplier = 3.0f;

    // 同期調整用パラメータ
    private float lipSyncOffsetMs = 0f;        // LipSync遅延補正（ミリ秒）
    private float lipSyncPredict = 0f;         // 予測補正（ミリ秒）
    private bool enableSyncOptimization = true; // 同期最適化の有効/無効

    private VRMLoader vrmLoader;
    private Vrm10RuntimeExpression expression;
    private FftProvider fftProvider;
    private float[] fftMagnitudes;
    private const FftSize fftSize = FftSize.Fft1024;

    private string lastPhoneme = "Aa";
    private float phonemeHoldTime = 0f;
    private const float phonemeSwitchInterval = 0.12f;


    private Dictionary<string, Vector2> GetDynamicBandRanges(Dictionary<string, float> phonemeRatios, float totalMin = 100f, float maxHz = 3000f) {
        Dictionary<string, Vector2> newRanges = new Dictionary<string, Vector2>();
        int phonemeCount = phonemeRatios.Count;
        float totalRange = maxHz;
        float minBandwidth = totalMin;
        float requiredMinTotal = phonemeCount * minBandwidth;

        if (totalRange < requiredMinTotal) {
            Debug.LogWarning("⚠️ 帯域幅が狭すぎてスライド調整できません！");
            return bandRanges; // fallback
        }

        // 正規化
        float totalRatio = 0f;
        foreach (var v in phonemeRatios.Values) totalRatio += v;
        if (totalRatio <= 0f) return bandRanges;

        Dictionary<string, float> normalizedRatios = new Dictionary<string, float>();
        foreach (var kv in phonemeRatios) {
            normalizedRatios[kv.Key] = kv.Value / totalRatio;
        }

        float remaining = totalRange - requiredMinTotal;
        float current = 0f;
        foreach (var kv in normalizedRatios) {
            float width = minBandwidth + kv.Value * remaining;
            newRanges[kv.Key] = new Vector2(current, current + width);
            current += width;
        }

        return newRanges;
    }
    private Dictionary<string, Vector2> bandRanges = new Dictionary<string, Vector2>()
    {
        /*
        { "Ou", new Vector2(0f, 500f) },
        { "Oh", new Vector2(500f, 1000f) },
        { "Aa", new Vector2(1000f, 1600f) },
        { "Ih", new Vector2(1600f, 2200f) },
        { "Ee", new Vector2(2200f, 3000f) },
        */
        { "Ou", new Vector2(0f, 250f) },
        { "Oh", new Vector2(250f, 500f) },
        { "Aa", new Vector2(500f, 2000f) },
        { "Ih", new Vector2(2000f, 2800f) },
        { "Ee", new Vector2(2800f, 3000f) },

    };

    private Dictionary<string, int> hitCounts = new Dictionary<string, int>()
    {
        { "Ou", 0 },
        { "Oh", 0 },
        { "Aa", 0 },
        { "Ih", 0 },
        { "Ee", 0 },
    };
    private Dictionary<string, float> currentWeights = new Dictionary<string, float>()
    {
        { "Aa", 0f },
        { "Ih", 0f },
        { "Ou", 0f },
        { "Ee", 0f },
        { "Oh", 0f }
    };

    // 前回フレームに適用したリップシンク値を保持し、表情値との加算を安定させる
    private Dictionary<string, float> lastLipValues = new Dictionary<string, float>()
    {
        { "Aa", 0f },
        { "Ih", 0f },
        { "Ou", 0f },
        { "Ee", 0f },
        { "Oh", 0f }
    };


    private void Start() {
        vrmLoader = FindAnyObjectByType<VRMLoader>();
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete += OnModelLoaded;
        }

        fftProvider = new FftProvider(1, fftSize);
        fftMagnitudes = new float[(int)fftSize];

        // 🔽 config から上書き（無効なものはスキップ）
        foreach (var entry in ServerConfig.Instance.lipSync.bandRanges) {
            if (entry.range != null && entry.range.Length == 2) {
                bandRanges[entry.key] = new Vector2(entry.range[0], entry.range[1]);
            }
            else {
                Debug.LogWarning($"⚠️ {entry.key} の設定が無効だったので、デフォルト値を使うよ～！");
            }
        }

        // 同期調整設定を読み込み
        var lipSyncConfig = ServerConfig.Instance.lipSync;
        lipSyncOffsetMs = lipSyncConfig.syncOffsetMs;
        lipSyncPredict = lipSyncConfig.syncPredictMs;
        enableSyncOptimization = lipSyncConfig.enableSyncOptimization;
        
        Debug.Log($"[LipSync] 同期設定を読み込み: オフセット={lipSyncOffsetMs}ms, 予測={lipSyncPredict}ms, 最適化={enableSyncOptimization}");
    }

    private void OnDestroy() {
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnModelLoaded;
        }
        StopLipSync();
    }


    private void OnModelLoaded(GameObject vrmModel) {
        expression = vrmLoader.VrmInstance.Runtime.Expression;
    }

    private bool IsVRMLoaded() {
        return vrmLoader != null && vrmLoader.VrmInstance != null && vrmLoader.VrmInstance.Runtime?.Expression != null;
    }
    private void Update() {
        if (!isLipSyncActive || fftProvider == null)
            return;

        int requiredLength = (int)fftSize;
        if (fftMagnitudes == null || fftMagnitudes.Length != requiredLength) {
            fftMagnitudes = new float[requiredLength]; // 💡 サイズ再確保
        }

        try {
            if (!fftProvider.GetFftData(fftMagnitudes)) {
                return; // データ不足ならスキップ
            }
        }
        catch (Exception ex) {
            Debug.LogWarning($"⚠️ FFTデータ取得に失敗: {ex.Message}");
            return;
        }

        // ✅ 複数音素比率＋スライドレンジ更新
        Dictionary<string, float> phonemeRatios = GetPhonemeFromFFT();

        if (phonemeRatios == null || phonemeRatios.Count == 0) {
            ApplyPhoneme(new Dictionary<string, float>()); // 全クリア
            return;
        }

        ApplyPhoneme(phonemeRatios);
    }


    private Dictionary<string, float> GetPhonemeFromFFT() {
        // 初期ベース帯域でまず集計
        Dictionary<string, float> energyMap = new Dictionary<string, float>();
        foreach (var key in bandRanges.Keys) {
            energyMap[key] = 0f;
        }

        for (int i = 0; i < fftMagnitudes.Length; i++) {
            float freq = i * AudioSettings.outputSampleRate / (int)fftSize;
            float mag = fftMagnitudes[i];

            foreach (var kv in bandRanges) {
                if (freq >= kv.Value.x && freq < kv.Value.y) {
                    energyMap[kv.Key] += mag;
                    break;
                }
            }
        }

        float total = 0f;
        foreach (var val in energyMap.Values) total += val;
        if (total < 0.001f) return new Dictionary<string, float>(); // 無音なら全部ゼロで返す

        // 比率として正規化
        Dictionary<string, float> phonemeRatios = new Dictionary<string, float>();
        foreach (var kv in energyMap) {
            phonemeRatios[kv.Key] = kv.Value / total;
        }

        // 🎯 新しいスライド最適化レンジを適用！
        bandRanges = GetDynamicBandRanges(phonemeRatios);

        return phonemeRatios;
    }

    private void ApplyPhoneme(Dictionary<string, float> phonemeRatios) {
        if (expression == null) return;

        float rms = (currentSource == LipSyncSource.WASAPI) ? wasapiVolume : GetMicrophoneVolume();
        float scaled = Mathf.Clamp01(rms * scaleMultiplier);

        // 同期調整機能を使用
        ApplyPhonemeWithSync(phonemeRatios, scaled);
    }

    // BLEND_EXPR モード: 現在の実装（表情との加算）
    private void ApplyBlendExprMode(Dictionary<string, float> phonemeRatios, float scaled) {
        float lerpSpeed = 0.2f;
        var keys = new List<string>(currentWeights.Keys);

        foreach (var key in keys) {
            float target = phonemeRatios.TryGetValue(key, out var ratio) ? ratio * scaled : 0f;
            float lerped = Mathf.Lerp(currentWeights[key], target, lerpSpeed);
            
            // ダイナミックレンジ調整: 強い値は圧縮、弱い値はそのまま
            float compressed = ApplyDynamicRange(lerped);
            currentWeights[key] = compressed;

            ExpressionKey exKey = GetExpressionKey(key);
            if (exKey.Preset != ExpressionPreset.custom) {
                float currentExp = expression.GetWeight(exKey);
                float baseValue = Mathf.Clamp01(currentExp - lastLipValues[key]);
                float finalValue = Mathf.Min(baseValue + compressed, 1.0f);
                expression.SetWeight(exKey, finalValue);
                lastLipValues[key] = compressed;
            }
        }
    }

    // RESET_EXPR モード: 一定入力後に表情をリセット
    private void ApplyResetExprMode(Dictionary<string, float> phonemeRatios, float scaled) {
        // まずBLEND_EXPRと同様に処理
        ApplyBlendExprMode(phonemeRatios, scaled);
        
        Debug.Log("!!! ApplyResetExprMode: scaled = " + scaled + ", lipSyncInputThreshold = " + lipSyncInputThreshold);


        // 入力レベルの監視とリセット処理
        if (scaled > lipSyncInputThreshold) {
            Debug.Log("!!! scaled pass");
            lipSyncInputDuration += Time.deltaTime;

            Debug.Log($"!!! Debug values: lipSyncInputDuration={lipSyncInputDuration}, lipSyncInputRequiredTime={lipSyncInputRequiredTime}, isWaitingForReset={isWaitingForReset}");

            if (lipSyncInputDuration >= lipSyncInputRequiredTime && !isWaitingForReset) {
                Debug.Log("!!!  lipSyncInputDuration - pass");
                isWaitingForReset = true;
                if (resetExpressionCoroutine != null) {
                    Debug.Log("!!!  resetExpressionCoroutine - pass");
                    StopCoroutine(resetExpressionCoroutine);
                }
                resetExpressionCoroutine = StartCoroutine(DelayedExpressionReset());
            }
        }
        else {
            lipSyncInputDuration = 0f;
        }
    }

    // NONE モード: 表情がある時はLipSyncを無効化
    private void ApplyNoneMode(Dictionary<string, float> phonemeRatios, float scaled) {
        if (HasActiveEmotionExpression()) {
            // 表情がアクティブな場合はLipSyncを適用しない
            ClearLipSyncValues();
            return;
        }
        
        // 表情がない場合は通常のLipSyncを適用
        ApplyBlendExprMode(phonemeRatios, scaled);
    }

    // FULL モード: 改良前の単純実装（表情との競合を考慮しない直接設定）
    private void ApplyFullMode(Dictionary<string, float> phonemeRatios, float scaled) {
        float lerpSpeed = 0.2f;
        var keys = new List<string>(currentWeights.Keys);

        foreach (var key in keys) {
            float target = phonemeRatios.TryGetValue(key, out var ratio) ? ratio * scaled : 0f;
            float lerped = Mathf.Lerp(currentWeights[key], target, lerpSpeed);
            
            // ダイナミックレンジ調整: 強い値は圧縮、弱い値はそのまま
            float compressed = ApplyDynamicRange(lerped);
            currentWeights[key] = compressed;

            switch (key) {
                case "Aa":
                    expression.SetWeight(ExpressionKey.Aa, compressed);
                    break;
                case "Ih":
                    expression.SetWeight(ExpressionKey.Ih, compressed);
                    break;
                case "Ou":
                    expression.SetWeight(ExpressionKey.Ou, compressed);
                    break;
                case "Ee":
                    expression.SetWeight(ExpressionKey.Ee, compressed);
                    break;
                case "Oh":
                    expression.SetWeight(ExpressionKey.Oh, compressed);
                    break;
            }
        }
    }

    // AUTO モードの処理
    private void HandleAutoMode() {
        if (Time.time - lastAutoModeChange >= autoModeChangeInterval) {
            // ランダムにBLEND_EXPRかRESET_EXPRを選択
            currentAutoMode = (UnityEngine.Random.value < 0.5f) ? 
                LipSyncBlendMode.LIPSYNC_MODE_BLEND_EXPR : 
                LipSyncBlendMode.LIPSYNC_MODE_RESET_EXPR;
            
            lastAutoModeChange = Time.time;
            Debug.Log($"[LipSync AUTO] モード切り替え: {currentAutoMode}");
        }
    }

    // 表情のリセット（遅延実行）
    private IEnumerator DelayedExpressionReset() {
        yield return new WaitForSeconds(lipSyncResetDelay);
        
        if (expression == null) {
            Debug.LogWarning("[LipSync RESET_EXPR] Expression が null のためリセットできません");
            isWaitingForReset = false;
            lipSyncInputDuration = 0f;
            yield break;
        }
        
        // 全ての表情をリセット（AnimationCommandHandlerの実装を参考）
        foreach (var key in expression.ExpressionKeys) {
            expression.SetWeight(key, 0.0f);
        }
        
        Debug.Log("[LipSync RESET_EXPR] 表情をリセットしました");
        isWaitingForReset = false;
        lipSyncInputDuration = 0f;
    }

    // アクティブな感情表情があるかチェック
    private bool HasActiveEmotionExpression() {
        if (expression == null) return false;
        
        foreach (var key in emotionExpressionKeys) {
            if (expression.GetWeight(key) > 0.01f) {
                return true;
            }
        }
        return false;
    }

    // LipSync値をクリア
    private void ClearLipSyncValues() {
        var keys = new List<string>(currentWeights.Keys);
        foreach (var key in keys) {
            currentWeights[key] = 0f;
            ExpressionKey exKey = GetExpressionKey(key);
            if (exKey.Preset != ExpressionPreset.custom) {
                float currentExp = expression.GetWeight(exKey);
                float baseValue = Mathf.Clamp01(currentExp - lastLipValues[key]);
                expression.SetWeight(exKey, baseValue);
                lastLipValues[key] = 0f;
            }
        }
    }

    // 文字列からExpressionKeyを取得
    private ExpressionKey GetExpressionKey(string key) {
        switch (key) {
            case "Aa": return ExpressionKey.Aa;
            case "Ih": return ExpressionKey.Ih;
            case "Ou": return ExpressionKey.Ou;
            case "Ee": return ExpressionKey.Ee;
            case "Oh": return ExpressionKey.Oh;
            default: return ExpressionKey.CreateFromPreset(ExpressionPreset.custom);
        }
    }

    // ダイナミックレンジ調整: 強い値は圧縮、弱い値はそのまま
    private float ApplyDynamicRange(float input) {
        if (input <= 0f) return 0f;
        
        // 閾値: この値以下はそのまま、以上は対数圧縮
        float threshold = 0.4f;
        
        if (input <= threshold) {
            // 弱い値はそのまま
            return input;
        } else {
            // 強い値は対数圧縮で自然に減衰
            // log(1 + x) を使用して滑らかな圧縮を実現
            float excess = input - threshold;
            float compressed = threshold + Mathf.Log(1f + excess * 2f) * 0.3f;
            return Mathf.Clamp01(compressed);
        }
    }



    private float GetMicrophoneVolume() {
        if (microphoneClip == null) return 0f;
        int micPos = Microphone.GetPosition(microphoneDevice) - sampleWindow;
        if (micPos < 0) return 0f;
        microphoneClip.GetData(samples, micPos);
        float sum = 0f;
        for (int i = 0; i < sampleWindow; i++) {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / sampleWindow);
    }

    private void StartLipSyncMic() {
        if (Microphone.devices.Length > 0) {
            microphoneDevice = Microphone.devices[0];
            microphoneClip = Microphone.Start(microphoneDevice, true, 1, 44100);
            isLipSyncActive = true;
            currentSource = LipSyncSource.Microphone;
            Debug.Log(i18nMsg.AUDIOSYNC_MIC_STARTED);
        }
        else {
            Debug.LogError(i18nMsg.AUDIOSYNC_MIC_NOT_FOUND);
        }
    }

    private void StartLipSyncWavePlayback() {
        try {
            // WavePlayback用のFFTプロバイダーを初期化
            fftProvider = new FftProvider(1, fftSize);
            fftMagnitudes = new float[(int)fftSize];
            
            isLipSyncActive = true;
            currentSource = LipSyncSource.WavePlayback;
            Debug.Log("[LipSync] Wave Playback チャンネルを開始しました");
        }
        catch (Exception ex) {
            Debug.LogError($"[LipSync] Wave Playback チャンネルの開始に失敗: {ex.Message}");
        }
    }

    private void StartLipSyncWASAPI() {
        try {
            wasapiCapture = new WasapiLoopbackCapture();
            wasapiCapture.Initialize();

            fftProvider = new FftProvider(1, fftSize);
            fftMagnitudes = new float[(int)fftSize];

            wasapiCapture.DataAvailable += WasapiCapture_DataAvailable;
            wasapiCapture.Start();
            isLipSyncActive = true;
            currentSource = LipSyncSource.WASAPI;
            Debug.Log(i18nMsg.AUDIOSYNC_WASAPI_STARTED);
        }
        catch (Exception ex) {
            Debug.LogError(string.Format(i18nMsg.AUDIOSYNC_WASAPI_START_FAILED, ex.Message));
        }
    }

    private void WasapiCapture_DataAvailable(object sender, DataAvailableEventArgs e) {
        int bytesPerSample = wasapiCapture.WaveFormat.BitsPerSample / 8;
        int sampleCount = e.ByteCount / bytesPerSample;

        float sum = 0f;
        float[] floatBuffer = new float[sampleCount];
        Buffer.BlockCopy(e.Data, 0, floatBuffer, 0, e.ByteCount);

        for (int i = 0; i < sampleCount; i++) {
            float sample = floatBuffer[i];
            sum += sample * sample;
            fftProvider?.Add(sample, 0);
        }

        float rms = (sampleCount > 0) ? Mathf.Sqrt(sum / sampleCount) : 0f;
        lock (wasapiLock) {
            wasapiVolume = rms;
        }
    }

    public void StartLipSync(int channel, float scale) {
        scaleMultiplier = scale;
        StartLipSync(channel);
    }

    public void StartLipSync(int channel) {
        if (isLipSyncActive) {
            Debug.LogWarning(i18nMsg.AUDIOSYNC_ALREADY_ACTIVE);
            StopLipSync();
        }

        if (!IsVRMLoaded()) {
            Debug.LogError("❌ VRM がロードされていないか、Expression システムが無効のため、リップシンクを開始できません！");
            return;
        }

        expression = vrmLoader.VrmInstance.Runtime.Expression;

        if (channel == MICROPHONE_CHANNEL_ID) {
            StartLipSyncMic();
        }
        else if (channel == WAVEPLAYBACK_CHANNEL_ID) {
            StartLipSyncWavePlayback();
        }
        else if (channel == WASAPI_CHANNEL_ID) {
            StartLipSyncWASAPI();
        }
        else {
            Debug.LogError(string.Format(i18nMsg.AUDIOSYNC_INVALID_CHANNEL, channel));
        }
    }

    public void StopLipSync() {
        if (!isLipSyncActive) return;

        if (currentSource == LipSyncSource.Microphone) {
            Microphone.End(microphoneDevice);
        }
        else if (currentSource == LipSyncSource.WavePlayback) {
            // WavePlayback用のクリーンアップ（必要に応じて）
            Debug.Log("[LipSync] Wave Playback チャンネルを停止しました");
        }
        else if (currentSource == LipSyncSource.WASAPI) {
            wasapiCapture?.Stop();
            wasapiCapture?.Dispose();
            wasapiCapture = null;
        }

        isLipSyncActive = false;
        currentSource = LipSyncSource.None;

        if (expression != null) {
            expression.SetWeight(ExpressionKey.Aa, 0f);
        }

        Debug.Log(i18nMsg.AUDIOSYNC_STOPPED);
    }

    // ブレンドモード制御用のパブリックメソッド
    public void SetLipSyncBlendMode(LipSyncBlendMode mode) {
        currentBlendMode = mode;
        Debug.Log($"[LipSync] ブレンドモードを変更: {mode}");
        
        // モード変更時のクリーンアップ
        if (resetExpressionCoroutine != null) {
            StopCoroutine(resetExpressionCoroutine);
            resetExpressionCoroutine = null;
        }
        isWaitingForReset = false;
        lipSyncInputDuration = 0f;
        
        // AUTOモードの場合は初期化
        if (mode == LipSyncBlendMode.LIPSYNC_MODE_AUTO) {
            lastAutoModeChange = Time.time;
            currentAutoMode = LipSyncBlendMode.LIPSYNC_MODE_BLEND_EXPR;
        }
    }

    public LipSyncBlendMode GetLipSyncBlendMode() {
        return currentBlendMode;
    }

    // RESET_EXPRモードのパラメータ設定
    public void SetResetExprParameters(float threshold = 0.3f, float requiredTime = 1.0f, float resetDelay = 1.0f) {
        lipSyncInputThreshold = threshold;
        lipSyncInputRequiredTime = requiredTime;
        lipSyncResetDelay = resetDelay;
        Debug.Log($"[LipSync RESET_EXPR] パラメータ更新: threshold={threshold}, requiredTime={requiredTime}, resetDelay={resetDelay}");
    }

    // AUTOモードのパラメータ設定
    public void SetAutoModeParameters(float changeInterval = 5.0f) {
        autoModeChangeInterval = changeInterval;
        Debug.Log($"[LipSync AUTO] パラメータ更新: changeInterval={changeInterval}");
    }

    // 表情を手動でリセット
    public void ResetExpressions() {
        if (expression == null) return;
        
        foreach (var key in emotionExpressionKeys) {
            expression.SetWeight(key, 0.0f);
        }
        
        Debug.Log("[LipSync] 表情を手動でリセットしました");
    }

    // 同期調整機能
    public void SetLipSyncOffset(float offsetMs) {
        lipSyncOffsetMs = offsetMs;
        Debug.Log($"[LipSync] 同期オフセットを設定: {offsetMs}ms");
    }

    public float GetLipSyncOffset() {
        return lipSyncOffsetMs;
    }

    public void SetLipSyncPredict(float predictMs) {
        lipSyncPredict = predictMs;
        Debug.Log($"[LipSync] 予測補正を設定: {predictMs}ms");
    }

    public float GetLipSyncPredict() {
        return lipSyncPredict;
    }

    public void SetSyncOptimization(bool enabled) {
        enableSyncOptimization = enabled;
        Debug.Log($"[LipSync] 同期最適化: {(enabled ? "有効" : "無効")}");
    }

    public bool GetSyncOptimization() {
        return enableSyncOptimization;
    }

    // 同期調整を適用したタイミングでフォニーム適用
    private void ApplyPhonemeWithSync(Dictionary<string, float> phonemeRatios, float scaled) {
        if (!enableSyncOptimization) {
            // 最適化無効の場合は従来通り
            ApplyPhonemeInternal(phonemeRatios, scaled);
            return;
        }

        // 同期調整を適用
        float totalOffsetMs = lipSyncOffsetMs + lipSyncPredict;
        
        if (Mathf.Abs(totalOffsetMs) < 1f) {
            // オフセットが小さい場合は直接適用
            ApplyPhonemeInternal(phonemeRatios, scaled);
        } else if (totalOffsetMs > 0) {
            // 遅延補正: 指定時間後に適用
            StartCoroutine(DelayedPhonemeApplication(phonemeRatios, scaled, totalOffsetMs / 1000f));
        } else {
            // 予測補正: 現在のフレームで早めに適用（簡易実装）
            ApplyPhonemeInternal(phonemeRatios, scaled);
        }
    }

    private IEnumerator DelayedPhonemeApplication(Dictionary<string, float> phonemeRatios, float scaled, float delaySeconds) {
        yield return new WaitForSeconds(delaySeconds);
        ApplyPhonemeInternal(phonemeRatios, scaled);
    }

    private void ApplyPhonemeInternal(Dictionary<string, float> phonemeRatios, float scaled) {
        if (expression == null) return;

        // AUTO モードの処理
        if (currentBlendMode == LipSyncBlendMode.LIPSYNC_MODE_AUTO) {
            HandleAutoMode();
        }

        // 現在のブレンドモードに応じて処理を分岐
        LipSyncBlendMode activeMode = (currentBlendMode == LipSyncBlendMode.LIPSYNC_MODE_AUTO) ? currentAutoMode : currentBlendMode;

        switch (activeMode) {
            case LipSyncBlendMode.LIPSYNC_MODE_BLEND_EXPR:
                ApplyBlendExprMode(phonemeRatios, scaled);
                break;
            case LipSyncBlendMode.LIPSYNC_MODE_RESET_EXPR:
                ApplyResetExprMode(phonemeRatios, scaled);
                break;
            case LipSyncBlendMode.LIPSYNC_MODE_NONE:
                ApplyNoneMode(phonemeRatios, scaled);
                break;
            case LipSyncBlendMode.LIPSYNC_MODE_FULL:
                ApplyFullMode(phonemeRatios, scaled);
                break;
        }
    }

    // WavePlayback用のサンプルデータ受信メソッド
    public void FeedWaveSample(float sample) {
        if (currentSource == LipSyncSource.WavePlayback && fftProvider != null) {
            fftProvider.Add(sample, 0);
        }
    }

    // WavePlayback用のRMS値受信メソッド
    public void FeedWaveRms(float rms) {
        if (currentSource == LipSyncSource.WavePlayback) {
            lock (wasapiLock) {
                wasapiVolume = rms;
            }
        }
    }

    public string GetLipSyncStatusJson() {
        AudioStatusInfo status = new AudioStatusInfo {
            currentSource = currentSource.ToString(),
            currentBlendMode = currentBlendMode.ToString(),
            currentAutoMode = (currentBlendMode == LipSyncBlendMode.LIPSYNC_MODE_AUTO) ? currentAutoMode.ToString() : null,
            hasActiveExpression = HasActiveEmotionExpression(),
            isWaitingForReset = isWaitingForReset,
            lipSyncInputDuration = lipSyncInputDuration,
            availableChannels = new List<AudioChannelInfo>()
            {
                new AudioChannelInfo() { id = WAVEPLAYBACK_CHANNEL_ID, name = "Wave Playback" },
                new AudioChannelInfo() { id = WASAPI_CHANNEL_ID, name = "System Audio (WASAPI)" },
                new AudioChannelInfo() { id = MICROPHONE_CHANNEL_ID, name = "Microphone" }
            }
        };
        return JsonUtility.ToJson(status);
    }

    [Serializable]
    public class AudioStatusInfo {
        public string currentSource;
        public string currentBlendMode;
        public string currentAutoMode;
        public bool hasActiveExpression;
        public bool isWaitingForReset;
        public float lipSyncInputDuration;
        public List<AudioChannelInfo> availableChannels;
    }

    [Serializable]
    public class AudioChannelInfo {
        public int id;
        public string name;
    }
}
