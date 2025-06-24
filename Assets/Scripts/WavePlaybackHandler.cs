using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System;

public class WavePlaybackHandler : MonoBehaviour {
    public static WavePlaybackHandler Instance { get; private set; }

    [Header("Wave Playback Configuration")]
    [SerializeField] private bool enableWavePlayback = true;
    [SerializeField] private string waveEndpoint = "/waveplay/";
    [SerializeField] private int maxBufferSize = 1024 * 1024; // 1MB buffer

    private AudioChannelManager audioChannelManager;
    private FFTAnalysisChannel fftAnalysisChannel;
    private Queue<float> audioBuffer = new Queue<float>();
    private bool isReceivingAudio = false;
    private Coroutine playbackCoroutine;

    // Audio format parameters
    private int sampleRate = 44100;
    private int channels = 1;
    private float[] tempAudioData;
    private AudioClip streamingClip;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
            // エディタ環境以外でのみDontDestroyOnLoadを適用
            #if !UNITY_EDITOR
            DontDestroyOnLoad(gameObject);
            #endif
            InitializeWavePlayback();
        } else {
            Destroy(gameObject);
        }
    }

    private void InitializeWavePlayback() {
        // Load configuration
        var config = ServerConfig.Instance;
        if (config?.wave_playback != null) {
            enableWavePlayback = config.wave_playback.enabled;
            waveEndpoint = config.wave_playback.endpoint;
        }

        // Get references to other systems
        audioChannelManager = AudioChannelManager.Instance;
        fftAnalysisChannel = FFTAnalysisChannel.Instance;

        // Create streaming audio clip
        streamingClip = AudioClip.Create("StreamingAudio", sampleRate * 10, channels, sampleRate, true, OnAudioRead);

        Debug.Log($"[WavePlaybackHandler] Initialized - Enabled: {enableWavePlayback}, Endpoint: {waveEndpoint}");
        Debug.Log($"[WavePlaybackHandler] AudioChannelManager: {(audioChannelManager != null ? "Found" : "Not Found")}");
        Debug.Log($"[WavePlaybackHandler] FFTAnalysisChannel: {(fftAnalysisChannel != null ? "Found" : "Not Found")}");
        Debug.Log($"[WavePlaybackHandler] StreamingClip: {(streamingClip != null ? $"Created ({streamingClip.length}s)" : "Failed to create")}");
    }

    /// <summary>
    /// Handle incoming wave data from HTTP endpoint
    /// </summary>
    public void HandleWaveData(HttpListenerContext context) {
        if (!enableWavePlayback) {
            SendResponse(context, 503, "Wave playback is disabled");
            return;
        }

        try {
            using (var reader = new BinaryReader(context.Request.InputStream)) {
                // Read wave header information from query parameters or headers
                string formatParam = context.Request.QueryString["format"] ?? "pcm";
                string sampleRateParam = context.Request.QueryString["sampleRate"] ?? "44100";
                string channelsParam = context.Request.QueryString["channels"] ?? "1";

                int.TryParse(sampleRateParam, out sampleRate);
                int.TryParse(channelsParam, out channels);

                // Read audio data
                byte[] audioData = reader.ReadBytes((int)context.Request.ContentLength64);
                
                if (audioData.Length > 0) {
                    ProcessAudioData(audioData, formatParam);
                    SendResponse(context, 200, $"Received {audioData.Length} bytes of audio data");
                } else {
                    SendResponse(context, 400, "No audio data received");
                }
            }
        }
        catch (Exception ex) {
            Debug.LogError($"[WavePlaybackHandler] Error processing wave data: {ex.Message}");
            SendResponse(context, 500, $"Error processing audio data: {ex.Message}");
        }
    }

    private void ProcessAudioData(byte[] audioData, string format) {
        float[] samples = null;

        switch (format.ToLower()) {
            case "pcm":
            case "wav":
                samples = ConvertPCMToFloat(audioData);
                break;
            case "float32":
                samples = ConvertFloat32ToFloat(audioData);
                break;
            default:
                Debug.LogWarning($"[WavePlaybackHandler] Unsupported audio format: {format}");
                return;
        }

        if (samples != null && samples.Length > 0) {
            EnqueueAudioSamples(samples);
            
            // Start playback if not already playing
            if (!isReceivingAudio) {
                StartAudioPlayback();
            }
        }
    }

    private float[] ConvertPCMToFloat(byte[] pcmData) {
        // Assume 16-bit PCM
        int sampleCount = pcmData.Length / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++) {
            short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            samples[i] = sample / 32768f; // Convert to -1.0 to 1.0 range
        }

        return samples;
    }

    private float[] ConvertFloat32ToFloat(byte[] float32Data) {
        int sampleCount = float32Data.Length / 4;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++) {
            samples[i] = BitConverter.ToSingle(float32Data, i * 4);
        }

        return samples;
    }

    private void EnqueueAudioSamples(float[] samples) {
        lock (audioBuffer) {
            // Prevent buffer overflow
            while (audioBuffer.Count + samples.Length > maxBufferSize) {
                audioBuffer.Dequeue();
            }

            // Add new samples to buffer
            foreach (float sample in samples) {
                audioBuffer.Enqueue(sample);
            }
        }

        Debug.Log($"[WavePlaybackHandler] Enqueued {samples.Length} samples, buffer size: {audioBuffer.Count}");
    }

    private void StartAudioPlayback() {
        if (isReceivingAudio) return;

        isReceivingAudio = true;

        // Ensure AudioChannelManager is available
        if (audioChannelManager == null) {
            audioChannelManager = AudioChannelManager.Instance;
        }

        // Start playback through AudioChannelManager
        if (audioChannelManager != null) {
            audioChannelManager.PlayAudioClip(streamingClip, 1f, false);
            Debug.Log("[WavePlaybackHandler] Started audio playback through AudioChannelManager");
        } else {
            // Fallback: Create a simple AudioSource for direct playback
            GameObject audioGO = new GameObject("WavePlaybackAudioSource");
            audioGO.transform.SetParent(transform);
            AudioSource audioSource = audioGO.AddComponent<AudioSource>();
            audioSource.clip = streamingClip;
            audioSource.volume = 1f;
            audioSource.Play();
            Debug.Log("[WavePlaybackHandler] Started audio playback with fallback AudioSource");
        }

        // Start FFT analysis if available
        if (fftAnalysisChannel != null) {
            // FFT analysis will automatically pick up the audio from the dedicated channel
        }

        Debug.Log("[WavePlaybackHandler] Started audio playback");
    }

    private void StopAudioPlayback() {
        if (!isReceivingAudio) return;

        isReceivingAudio = false;

        // Stop playback
        if (audioChannelManager != null) {
            audioChannelManager.StopAllChannels();
        }

        // Clear buffer
        lock (audioBuffer) {
            audioBuffer.Clear();
        }

        Debug.Log("[WavePlaybackHandler] Stopped audio playback");
    }

    /// <summary>
    /// Unity's audio callback for streaming audio
    /// </summary>
    private void OnAudioRead(float[] data) {
        lock (audioBuffer) {
            int samplesProvided = 0;
            for (int i = 0; i < data.Length; i++) {
                if (audioBuffer.Count > 0) {
                    data[i] = audioBuffer.Dequeue();
                    samplesProvided++;
                } else {
                    data[i] = 0f; // Silence if no data available
                }
            }

            // Debug log every 1000 calls to avoid spam
            if (Time.frameCount % 1000 == 0 && samplesProvided > 0) {
                Debug.Log($"[WavePlaybackHandler] OnAudioRead: provided {samplesProvided}/{data.Length} samples, buffer: {audioBuffer.Count}");
            }

            // Stop playback if buffer is empty
            if (audioBuffer.Count == 0 && isReceivingAudio) {
                // Don't stop immediately, allow for brief gaps
                StartCoroutine(CheckForPlaybackStop());
            }
        }
    }

    private IEnumerator CheckForPlaybackStop() {
        yield return new WaitForSeconds(1f); // Wait 1 second

        lock (audioBuffer) {
            if (audioBuffer.Count == 0) {
                StopAudioPlayback();
            }
        }
    }

    private void SendResponse(HttpListenerContext context, int statusCode, string message) {
        try {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain";
            
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(message);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception ex) {
            Debug.LogError($"[WavePlaybackHandler] Error sending response: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current playback status
    /// </summary>
    public WavePlaybackStatus GetStatus() {
        return new WavePlaybackStatus {
            enabled = enableWavePlayback,
            isReceiving = isReceivingAudio,
            bufferSize = audioBuffer.Count,
            maxBufferSize = maxBufferSize,
            sampleRate = sampleRate,
            channels = channels,
            endpoint = waveEndpoint
        };
    }

    /// <summary>
    /// Enable or disable wave playback
    /// </summary>
    public void SetEnabled(bool enabled) {
        enableWavePlayback = enabled;
        if (!enabled && isReceivingAudio) {
            StopAudioPlayback();
        }
        Debug.Log($"[WavePlaybackHandler] Wave playback {(enabled ? "enabled" : "disabled")}");
    }

    private void OnDestroy() {
        StopAudioPlayback();
        if (Instance == this) {
            Instance = null;
        }
    }
}

[System.Serializable]
public class WavePlaybackStatus {
    public bool enabled;
    public bool isReceiving;
    public int bufferSize;
    public int maxBufferSize;
    public int sampleRate;
    public int channels;
    public string endpoint;
}
