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
    private const int MICROPHONE_CHANNEL_ID = 1;
    private const int WASAPI_CHANNEL_ID = 3;
    private enum LipSyncSource { None, Microphone, WASAPI }
    private LipSyncSource currentSource = LipSyncSource.None;

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


    // mouth shape open limits to keep vertical mouth movements natural
    private readonly Dictionary<ExpressionKey, float> mouthShapeLimits = new Dictionary<ExpressionKey, float>
    {
        { ExpressionKey.Aa, 0.4f },
        { ExpressionKey.Ee, 0.5f },
        { ExpressionKey.Oh, 0.5f }
    };

    // blending weight for how strongly lip sync affects current expression
    private float lipSyncBlendWeight = 0.7f;



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

        float lerpSpeed = 0.2f;

        // 🔒 コピーして列挙安全に！
        var keys = new List<string>(currentWeights.Keys);

        foreach (var key in keys) {
            float target = phonemeRatios.TryGetValue(key, out var ratio) ? ratio * scaled : 0f;
            float lerped = Mathf.Lerp(currentWeights[key], target, lerpSpeed);
            currentWeights[key] = lerped;

            ExpressionKey exKey = default;
            bool valid = true;
            switch (key) {
                case "Aa": exKey = ExpressionKey.Aa; break;
                case "Ih": exKey = ExpressionKey.Ih; break;
                case "Ou": exKey = ExpressionKey.Ou; break;
                case "Ee": exKey = ExpressionKey.Ee; break;
                case "Oh": exKey = ExpressionKey.Oh; break;
                default:
                    valid = false;
                    break;
            }

            if (valid) {

                float lipValue = lerped * lipSyncBlendWeight;
                float currentExp = expression.GetWeight(exKey);
                float baseValue = Mathf.Clamp01(currentExp - lastLipValues[key]);
                float finalValue = Mathf.Min(baseValue + lipValue, 1.0f);
                if (mouthShapeLimits.TryGetValue(exKey, out var limit))
                {
                    finalValue = Mathf.Min(finalValue, limit);
                }
                expression.SetWeight(exKey, finalValue);
                lastLipValues[key] = lipValue;
            }
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

        if (vrmLoader?.VrmInstance?.Runtime?.Expression == null) {
            Debug.LogError("❌ VRM がロードされていないか、Expression システムが無効のため、リップシンクを開始できません！");
            return;
        }

        expression = vrmLoader.VrmInstance.Runtime.Expression;

        if (channel == MICROPHONE_CHANNEL_ID) {
            StartLipSyncMic();
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

    public string GetLipSyncStatusJson() {
        AudioStatusInfo status = new AudioStatusInfo {
            currentSource = currentSource.ToString(),
            availableChannels = new List<AudioChannelInfo>()
            {
                new AudioChannelInfo() { id = MICROPHONE_CHANNEL_ID, name = "Microphone" },
                new AudioChannelInfo() { id = WASAPI_CHANNEL_ID, name = "System Audio (WASAPI)" }
            }
        };
        return JsonUtility.ToJson(status);
    }

    [Serializable]
    public class AudioStatusInfo {
        public string currentSource;
        public List<AudioChannelInfo> availableChannels;
    }

    [Serializable]
    public class AudioChannelInfo {
        public int id;
        public string name;
    }
}
