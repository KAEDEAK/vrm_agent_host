using UnityEngine;
using UniVRM10;
using System.Collections.Generic;
using System;

public class FFTAnalysisChannel : MonoBehaviour {
    public static FFTAnalysisChannel Instance { get; private set; }

    [Header("FFT Configuration")]
    [SerializeField] private int fftSize = 1024;
    [SerializeField] private FFTWindow fftWindow = FFTWindow.BlackmanHarris;
    [SerializeField] private int audioChannel = 0;

    [Header("Phoneme Analysis")]
    [SerializeField] private float smoothingFactor = 0.2f;
    [SerializeField] private float scaleMultiplier = 3.0f;
    [SerializeField] private bool enableDynamicBandAdjustment = true;

    private AudioSource targetAudioSource;
    private float[] spectrumData;
    private VRMLoader vrmLoader;
    private Vrm10RuntimeExpression expression;

    // Phoneme frequency bands (Hz)
    private Dictionary<string, Vector2> bandRanges = new Dictionary<string, Vector2>() {
        { "Ou", new Vector2(0f, 250f) },
        { "Oh", new Vector2(250f, 500f) },
        { "Aa", new Vector2(500f, 2000f) },
        { "Ih", new Vector2(2000f, 2800f) },
        { "Ee", new Vector2(2800f, 3000f) },
    };

    private Dictionary<string, float> currentWeights = new Dictionary<string, float>() {
        { "Aa", 0f }, { "Ih", 0f }, { "Ou", 0f }, { "Ee", 0f }, { "Oh", 0f }
    };

    private Dictionary<string, float> lastLipValues = new Dictionary<string, float>() {
        { "Aa", 0f }, { "Ih", 0f }, { "Ou", 0f }, { "Ee", 0f }, { "Oh", 0f }
    };

    // Events
    public event Action<Dictionary<string, float>> OnPhonemeWeightsUpdated;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
            // エディタ環境以外でのみDontDestroyOnLoadを適用
            #if !UNITY_EDITOR
            DontDestroyOnLoad(gameObject);
            #endif
            InitializeFFTAnalysis();
        } else {
            Destroy(gameObject);
        }
    }

    private void InitializeFFTAnalysis() {
        spectrumData = new float[fftSize];
        
        // Get VRM loader reference
        vrmLoader = FindAnyObjectByType<VRMLoader>();
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete += OnModelLoaded;
        }

        // Subscribe to AudioChannelManager events
        if (AudioChannelManager.Instance != null) {
            AudioChannelManager.Instance.OnAudioClipChanged += OnAudioClipChanged;
            AudioChannelManager.Instance.OnPlaybackStateChanged += OnPlaybackStateChanged;
            
            // Get the FFT analysis audio source
            targetAudioSource = AudioChannelManager.Instance.GetFFTAnalysisAudioSource();
        }

        // Load configuration from ServerConfig if available
        LoadConfigurationFromServer();

        Debug.Log("[FFTAnalysisChannel] Initialized for phoneme analysis");
    }

    private void LoadConfigurationFromServer() {
        var config = ServerConfig.Instance;
        if (config?.lipSync?.bandRanges != null) {
            foreach (var entry in config.lipSync.bandRanges) {
                if (entry.range != null && entry.range.Length == 2) {
                    bandRanges[entry.key] = new Vector2(entry.range[0], entry.range[1]);
                } else {
                    Debug.LogWarning($"[FFTAnalysisChannel] Invalid band range for {entry.key}, using default");
                }
            }
        }
    }

    private void OnModelLoaded(GameObject vrmModel) {
        if (vrmLoader?.VrmInstance?.Runtime?.Expression != null) {
            expression = vrmLoader.VrmInstance.Runtime.Expression;
            Debug.Log("[FFTAnalysisChannel] VRM expression system connected");
        }
    }

    private void OnAudioClipChanged(AudioClip clip) {
        // Reset analysis when new clip starts
        ResetPhonemeWeights();
        Debug.Log($"[FFTAnalysisChannel] Audio clip changed: {clip?.name ?? "None"}");
    }

    private void OnPlaybackStateChanged(bool isPlaying) {
        if (!isPlaying) {
            ResetPhonemeWeights();
            ApplyPhonemeWeights();
        }
    }

    private void Update() {
        if (targetAudioSource == null || !targetAudioSource.isPlaying) {
            return;
        }

        PerformFFTAnalysis();
    }

    private void PerformFFTAnalysis() {
        // Get spectrum data from the dedicated FFT analysis audio source
        targetAudioSource.GetSpectrumData(spectrumData, audioChannel, fftWindow);

        // Analyze phonemes from spectrum data
        Dictionary<string, float> phonemeRatios = AnalyzePhonemes(spectrumData);

        // Apply smoothing and scaling
        ApplySmoothing(phonemeRatios);

        // Apply to VRM expression if available
        ApplyPhonemeWeights();

        // Notify listeners
        OnPhonemeWeightsUpdated?.Invoke(new Dictionary<string, float>(currentWeights));
    }

    private Dictionary<string, float> AnalyzePhonemes(float[] spectrum) {
        Dictionary<string, float> energyMap = new Dictionary<string, float>();
        
        // Initialize energy map
        foreach (var key in bandRanges.Keys) {
            energyMap[key] = 0f;
        }

        // Calculate frequency resolution
        float frequencyResolution = AudioSettings.outputSampleRate / 2f / spectrum.Length;

        // Analyze spectrum data
        for (int i = 0; i < spectrum.Length; i++) {
            float frequency = i * frequencyResolution;
            float magnitude = spectrum[i];

            // Find which phoneme band this frequency belongs to
            foreach (var band in bandRanges) {
                if (frequency >= band.Value.x && frequency < band.Value.y) {
                    energyMap[band.Key] += magnitude;
                    break;
                }
            }
        }

        // Normalize to ratios
        float totalEnergy = 0f;
        foreach (var energy in energyMap.Values) {
            totalEnergy += energy;
        }

        Dictionary<string, float> phonemeRatios = new Dictionary<string, float>();
        if (totalEnergy > 0.001f) {
            foreach (var kv in energyMap) {
                phonemeRatios[kv.Key] = kv.Value / totalEnergy;
            }

            // Dynamic band adjustment if enabled
            if (enableDynamicBandAdjustment) {
                bandRanges = GetDynamicBandRanges(phonemeRatios);
            }
        } else {
            // No audio detected, return zero ratios
            foreach (var key in bandRanges.Keys) {
                phonemeRatios[key] = 0f;
            }
        }

        return phonemeRatios;
    }

    private Dictionary<string, Vector2> GetDynamicBandRanges(Dictionary<string, float> phonemeRatios, float totalMin = 100f, float maxHz = 3000f) {
        Dictionary<string, Vector2> newRanges = new Dictionary<string, Vector2>();
        int phonemeCount = phonemeRatios.Count;
        float totalRange = maxHz;
        float minBandwidth = totalMin;
        float requiredMinTotal = phonemeCount * minBandwidth;

        if (totalRange < requiredMinTotal) {
            Debug.LogWarning("[FFTAnalysisChannel] Frequency range too narrow for dynamic adjustment");
            return bandRanges; // Return original ranges
        }

        // Normalize ratios
        float totalRatio = 0f;
        foreach (var v in phonemeRatios.Values) totalRatio += v;
        if (totalRatio <= 0f) return bandRanges;

        Dictionary<string, float> normalizedRatios = new Dictionary<string, float>();
        foreach (var kv in phonemeRatios) {
            normalizedRatios[kv.Key] = kv.Value / totalRatio;
        }

        // Calculate new ranges
        float remaining = totalRange - requiredMinTotal;
        float current = 0f;
        foreach (var kv in normalizedRatios) {
            float width = minBandwidth + kv.Value * remaining;
            newRanges[kv.Key] = new Vector2(current, current + width);
            current += width;
        }

        return newRanges;
    }

    private void ApplySmoothing(Dictionary<string, float> phonemeRatios) {
        var keys = new List<string>(currentWeights.Keys);
        
        foreach (var key in keys) {
            float target = phonemeRatios.TryGetValue(key, out var ratio) ? ratio * scaleMultiplier : 0f;
            float smoothed = Mathf.Lerp(currentWeights[key], target, smoothingFactor);
            
            // Apply dynamic range compression
            currentWeights[key] = ApplyDynamicRange(smoothed);
        }
    }

    private float ApplyDynamicRange(float input) {
        if (input <= 0f) return 0f;
        
        float threshold = 0.4f;
        
        if (input <= threshold) {
            return input;
        } else {
            float excess = input - threshold;
            float compressed = threshold + Mathf.Log(1f + excess * 2f) * 0.3f;
            return Mathf.Clamp01(compressed);
        }
    }

    private void ApplyPhonemeWeights() {
        if (expression == null) return;

        foreach (var kv in currentWeights) {
            ExpressionKey exKey = GetExpressionKey(kv.Key);
            if (exKey.Preset != ExpressionPreset.custom) {
                float currentExp = expression.GetWeight(exKey);
                float baseValue = Mathf.Clamp01(currentExp - lastLipValues[kv.Key]);
                float finalValue = Mathf.Min(baseValue + kv.Value, 1.0f);
                expression.SetWeight(exKey, finalValue);
                lastLipValues[kv.Key] = kv.Value;
            }
        }
    }

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

    private void ResetPhonemeWeights() {
        var keys = new List<string>(currentWeights.Keys);
        foreach (var key in keys) {
            currentWeights[key] = 0f;
            lastLipValues[key] = 0f;
        }
    }

    /// <summary>
    /// Get current phoneme weights
    /// </summary>
    public Dictionary<string, float> GetCurrentPhonemeWeights() {
        return new Dictionary<string, float>(currentWeights);
    }

    /// <summary>
    /// Set FFT analysis parameters
    /// </summary>
    public void SetFFTParameters(int size, FFTWindow window, float smoothing, float scale) {
        fftSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(size, 64, 8192));
        fftWindow = window;
        smoothingFactor = Mathf.Clamp01(smoothing);
        scaleMultiplier = Mathf.Max(0f, scale);
        
        spectrumData = new float[fftSize];
        Debug.Log($"[FFTAnalysisChannel] FFT parameters updated - Size: {fftSize}, Window: {fftWindow}");
    }

    /// <summary>
    /// Set phoneme band ranges
    /// </summary>
    public void SetPhonemeBandRanges(Dictionary<string, Vector2> ranges) {
        if (ranges != null) {
            bandRanges = new Dictionary<string, Vector2>(ranges);
            Debug.Log("[FFTAnalysisChannel] Phoneme band ranges updated");
        }
    }

    /// <summary>
    /// Get current band ranges
    /// </summary>
    public Dictionary<string, Vector2> GetPhonemeBandRanges() {
        return new Dictionary<string, Vector2>(bandRanges);
    }

    private void OnDestroy() {
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnModelLoaded;
        }

        if (AudioChannelManager.Instance != null) {
            AudioChannelManager.Instance.OnAudioClipChanged -= OnAudioClipChanged;
            AudioChannelManager.Instance.OnPlaybackStateChanged -= OnPlaybackStateChanged;
        }

        if (Instance == this) {
            Instance = null;
        }
    }
}
