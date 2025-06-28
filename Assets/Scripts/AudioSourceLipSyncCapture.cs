using UnityEngine;
using CSCore;
using CSCore.DSP;

/// <summary>
/// AudioSourceにアタッチして、実際に再生される音声データをリアルタイムでキャプチャし、
/// LipSyncシステムに送信するコンポーネント
/// </summary>
public class AudioSourceLipSyncCapture : MonoBehaviour {
    [Header("LipSync Settings")]
    [SerializeField] private bool enableLipSync = true;
    [SerializeField] private float rmsSmoothing = 0.1f;
    [SerializeField] private float volumeMultiplier = 15.0f; // WavePlayback用に最適化されたデフォルト値
    
    [Header("FFT Settings")]
    [SerializeField] private bool enableFFTAnalysis = true;
    private const FftSize fftSize = FftSize.Fft1024;
    
    private AudioLipSync lipSync;
    private FftProvider fftProvider;
    private float[] fftMagnitudes;
    private float smoothedRms = 0f;
    private bool isInitialized = false;
    
    // デバッグ用
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    private float lastRms = 0f;
    private int sampleCount = 0;

    private void Start() {
        InitializeComponents();
    }

    private void InitializeComponents() {
        // AudioLipSyncコンポーネントを取得
        lipSync = FindAnyObjectByType<AudioLipSync>();
        if (lipSync == null) {
            Debug.LogWarning("[AudioSourceLipSyncCapture] AudioLipSync component not found!");
            enableLipSync = false;
        }

        // FFTプロバイダーを初期化
        if (enableFFTAnalysis) {
            try {
                fftProvider = new FftProvider(1, fftSize);
                fftMagnitudes = new float[(int)fftSize];
                Debug.Log("[AudioSourceLipSyncCapture] FFT Provider initialized successfully");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[AudioSourceLipSyncCapture] Failed to initialize FFT Provider: {ex.Message}");
                enableFFTAnalysis = false;
            }
        }

        isInitialized = true;
        Debug.Log("[AudioSourceLipSyncCapture] Component initialized successfully");
    }

    /// <summary>
    /// AudioSourceから実際に出力される音声データをリアルタイムでキャプチャ
    /// </summary>
    /// <param name="data">音声データ配列</param>
    /// <param name="channels">チャンネル数</param>
    private void OnAudioFilterRead(float[] data, int channels) {
        if (!isInitialized || !enableLipSync || lipSync == null) {
            return;
        }

        try {
            // RMS値を計算
            float rms = CalculateRMS(data, channels);
            
            // スムージング適用
            smoothedRms = Mathf.Lerp(smoothedRms, rms, rmsSmoothing);
            
            // ボリューム調整
            float adjustedRms = smoothedRms * volumeMultiplier;
            
            // LipSyncシステムに送信
            lipSync.FeedWaveRms(adjustedRms);
            
            // デバッグログ（Volume Multiplierの効果を確認）
            if (showDebugInfo && sampleCount % 100 == 0) {
                Debug.Log($"[AudioSourceLipSyncCapture] Raw RMS: {rms:F4}, Smoothed: {smoothedRms:F4}, Adjusted: {adjustedRms:F4}, Multiplier: {volumeMultiplier:F2}");
            }
            
            // FFT解析（有効な場合）
            if (enableFFTAnalysis && fftProvider != null) {
                FeedFFTData(data, channels);
            }
            
            // デバッグ情報更新
            if (showDebugInfo) {
                lastRms = adjustedRms;
                sampleCount++;
            }
        }
        catch (System.Exception ex) {
            Debug.LogError($"[AudioSourceLipSyncCapture] Error in OnAudioFilterRead: {ex.Message}");
        }
    }

    /// <summary>
    /// RMS（Root Mean Square）値を計算
    /// </summary>
    /// <param name="data">音声データ</param>
    /// <param name="channels">チャンネル数</param>
    /// <returns>RMS値</returns>
    private float CalculateRMS(float[] data, int channels) {
        if (data == null || data.Length == 0) {
            return 0f;
        }

        float sum = 0f;
        int sampleCount = data.Length;

        // ステレオの場合はモノラルに変換
        if (channels == 2) {
            for (int i = 0; i < data.Length; i += 2) {
                float monoSample = (data[i] + data[i + 1]) * 0.5f;
                sum += monoSample * monoSample;
            }
            sampleCount = data.Length / 2;
        }
        else {
            // モノラルの場合
            for (int i = 0; i < data.Length; i++) {
                sum += data[i] * data[i];
            }
        }

        return sampleCount > 0 ? Mathf.Sqrt(sum / sampleCount) : 0f;
    }

    /// <summary>
    /// FFT解析用にデータを送信
    /// </summary>
    /// <param name="data">音声データ</param>
    /// <param name="channels">チャンネル数</param>
    private void FeedFFTData(float[] data, int channels) {
        if (lipSync == null) return;

        try {
            // ステレオの場合はモノラルに変換してAudioLipSyncに送信
            if (channels == 2) {
                for (int i = 0; i < data.Length; i += 2) {
                    float monoSample = (data[i] + data[i + 1]) * 0.5f;
                    lipSync.FeedFFTData(monoSample);
                }
            }
            else {
                // モノラルの場合はそのままAudioLipSyncに送信
                for (int i = 0; i < data.Length; i++) {
                    lipSync.FeedFFTData(data[i]);
                }
            }
        }
        catch (System.Exception ex) {
            Debug.LogWarning($"[AudioSourceLipSyncCapture] FFT data feed error: {ex.Message}");
        }
    }

    /// <summary>
    /// LipSyncの有効/無効を切り替え
    /// </summary>
    /// <param name="enabled">有効にするかどうか</param>
    public void SetLipSyncEnabled(bool enabled) {
        enableLipSync = enabled;
        
        if (!enabled && lipSync != null) {
            // LipSyncを無効にする場合は0を送信してクリア
            lipSync.FeedWaveRms(0f);
        }
        
        Debug.Log($"[AudioSourceLipSyncCapture] LipSync {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// ボリューム倍率を設定
    /// </summary>
    /// <param name="multiplier">倍率</param>
    public void SetVolumeMultiplier(float multiplier) {
        volumeMultiplier = Mathf.Clamp(multiplier, 0f, 100f); // 範囲を拡張
        Debug.Log($"[AudioSourceLipSyncCapture] Volume multiplier set to {volumeMultiplier}");
    }

    /// <summary>
    /// スムージング値を設定
    /// </summary>
    /// <param name="smoothing">スムージング値（0-1）</param>
    public void SetRmsSmoothing(float smoothing) {
        rmsSmoothing = Mathf.Clamp01(smoothing);
        Debug.Log($"[AudioSourceLipSyncCapture] RMS smoothing set to {rmsSmoothing}");
    }

    /// <summary>
    /// 現在のRMS値を取得
    /// </summary>
    /// <returns>現在のRMS値</returns>
    public float GetCurrentRms() {
        return smoothedRms;
    }

    /// <summary>
    /// FFT解析の有効/無効を切り替え
    /// </summary>
    /// <param name="enabled">有効にするかどうか</param>
    public void SetFFTAnalysisEnabled(bool enabled) {
        enableFFTAnalysis = enabled;
        Debug.Log($"[AudioSourceLipSyncCapture] FFT Analysis {(enabled ? "enabled" : "disabled")}");
    }

    private void OnDestroy() {
        // クリーンアップ
        if (lipSync != null) {
            lipSync.FeedWaveRms(0f);
        }
        
        // FftProviderはIDisposableを実装していないため、単純にnullに設定
        fftProvider = null;
        
        Debug.Log("[AudioSourceLipSyncCapture] Component destroyed and cleaned up");
    }

    // デバッグ情報表示用
    private void OnGUI() {
        if (!showDebugInfo || !isInitialized) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("AudioSource LipSync Capture Debug");
        GUILayout.Label($"LipSync Enabled: {enableLipSync}");
        GUILayout.Label($"Current RMS: {lastRms:F4}");
        GUILayout.Label($"Smoothed RMS: {smoothedRms:F4}");
        GUILayout.Label($"Volume Multiplier: {volumeMultiplier:F2}");
        GUILayout.Label($"Sample Count: {sampleCount}");
        GUILayout.Label($"FFT Analysis: {enableFFTAnalysis}");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
