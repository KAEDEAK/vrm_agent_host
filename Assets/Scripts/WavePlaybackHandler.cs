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
    [SerializeField] private float wavePlaybackVolume = 1.0f; // 音量制御用

    private AudioChannelManager audioChannelManager;
    private FFTAnalysisChannel fftAnalysisChannel;
    private Queue<float> audioBuffer = new Queue<float>();
    private bool isReceivingAudio = false;
    private Coroutine playbackCoroutine;
    
    // Queue management for concurrency control
    private Queue<WavePlaybackItem> waveQueue = new Queue<WavePlaybackItem>();
    private readonly object queueLock = new object();
    private const int MAX_QUEUE_SIZE = 10;
    private bool isProcessingQueue = false;

    // Audio format parameters
    private int sampleRate = 44100;
    private int channels = 1;
    private float[] tempAudioData;
    private AudioClip streamingClip;
    
    // Avatar head tracking
    private VRMLoader vrmLoader;
    private Transform avatarHeadTransform;
    private AudioSource spatialAudioSource; // アバターの頭に配置する音源

    private void Awake() {
        // Editor環境での実行を防止（追加の安全対策）
        #if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying) {
            Debug.LogWarning("[WavePlaybackHandler] Attempted to initialize in Editor non-play mode - destroying");
            Destroy(gameObject);
            return;
        }
        #endif
        
        // Runtime環境でのみ実行を許可
        if (!Application.isPlaying) {
            Debug.LogWarning("[WavePlaybackHandler] Attempted to initialize when Application.isPlaying is false - destroying");
            Destroy(gameObject);
            return;
        }

        if (Instance == null) {
            Instance = this;
            // エディタ環境以外でのみDontDestroyOnLoadを適用
            #if !UNITY_EDITOR
            DontDestroyOnLoad(gameObject);
            #endif
            InitializeWavePlayback();
            Debug.Log("[WavePlaybackHandler] Successfully initialized in runtime mode");
        } else {
            Debug.LogWarning("[WavePlaybackHandler] Instance already exists - destroying duplicate");
            Destroy(gameObject);
        }
    }

    private void InitializeWavePlayback() {
        // 追加の安全チェック：Editor環境での初期化を防止
        #if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying) {
            Debug.LogError("[WavePlaybackHandler] InitializeWavePlayback called in Editor non-play mode - aborting");
            return;
        }
        #endif
        
        if (!Application.isPlaying) {
            Debug.LogError("[WavePlaybackHandler] InitializeWavePlayback called when Application.isPlaying is false - aborting");
            return;
        }

        // Load configuration
        var config = ServerConfig.Instance;
        if (config != null) {
            enableWavePlayback = config.wavePlaybackEnabled;
            waveEndpoint = "/waveplay/";
        } else {
            Debug.LogWarning("[WavePlaybackHandler] ServerConfig.Instance is null - using default settings");
            enableWavePlayback = false; // デフォルトで無効化
        }

        // Get references to other systems
        audioChannelManager = AudioChannelManager.Instance;
        fftAnalysisChannel = FFTAnalysisChannel.Instance;
        vrmLoader = FindObjectOfType<VRMLoader>();

        // Subscribe to VRM load events
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete += OnVRMLoadComplete;
        }

        // Create streaming audio clip
        streamingClip = AudioClip.Create("StreamingAudio", sampleRate * 10, channels, sampleRate, true, OnAudioRead);

        // Initialize spatial audio source for avatar head positioning
        InitializeSpatialAudioSource();

        DebugLogger.LogVerbose($"[WavePlaybackHandler] Initialized - Enabled: {enableWavePlayback}, Endpoint: {waveEndpoint}");
        DebugLogger.LogVerbose($"[WavePlaybackHandler] AudioChannelManager: {(audioChannelManager != null ? "Found" : "Not Found")}");
        DebugLogger.LogVerbose($"[WavePlaybackHandler] FFTAnalysisChannel: {(fftAnalysisChannel != null ? "Found" : "Not Found")}");
        DebugLogger.LogVerbose($"[WavePlaybackHandler] VRMLoader: {(vrmLoader != null ? "Found" : "Not Found")}");
        DebugLogger.LogVerbose($"[WavePlaybackHandler] StreamingClip: {(streamingClip != null ? $"Created ({streamingClip.length}s)" : "Failed to create")}");
    }

    /// <summary>
    /// Called when a new VRM model is loaded
    /// </summary>
    private void OnVRMLoadComplete(GameObject model) {
        Debug.Log("[WavePlaybackHandler] New VRM model loaded, updating avatar head position");
        avatarHeadTransform = null; // Reset current head transform
        UpdateAvatarHeadPosition(); // Find new head position
    }

    /// <summary>
    /// Handle incoming wave data from HTTP endpoint with concurrency control
    /// </summary>
    public void HandleWaveData(HttpListenerContext context) {
        if (!enableWavePlayback) {
            SendJsonResponse(context, 503, "Wave playback is disabled");
            return;
        }

        try {
            // Handle GET requests (ping)
            if (context.Request.HttpMethod == "GET") {
                SendJsonResponse(context, 200, "ok");
                return;
            }

            // Handle POST requests (audio data)
            if (context.Request.HttpMethod != "POST") {
                SendJsonResponse(context, 405, "method_not_allowed");
                return;
            }

            // Validate content type
            if (!context.Request.ContentType?.StartsWith("audio/wav") ?? true) {
                SendJsonResponse(context, 415, "unsupported_media_type");
                return;
            }

            long contentLength = context.Request.ContentLength64;
            var config = ServerConfig.Instance;
            int maxBytes = config?.wavePayloadMaxBytes ?? 5000000;
            if (contentLength <= 0 || contentLength > maxBytes) {
                SendJsonResponse(context, 413, "payload_too_large");
                return;
            }

            // Extract headers
            string audioId = context.Request.Headers["X-Audio-ID"];
            if (string.IsNullOrEmpty(audioId)) audioId = Guid.NewGuid().ToString();

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

            // Read audio data
            byte[] audioData = new byte[contentLength];
            int totalRead = 0;
            using (var stream = context.Request.InputStream) {
                while (totalRead < contentLength) {
                    int read = stream.Read(audioData, totalRead, (int)(contentLength - totalRead));
                    if (read == 0) break;
                    totalRead += read;
                }
            }

            if (totalRead > 0) {
                // Create wave item and handle based on concurrency mode
                var waveItem = new WavePlaybackItem {
                    audioData = audioData,
                    context = context,
                    audioId = audioId,
                    volume = volume,
                    spatialOverride = spatialOverride,
                    enqueuedTime = DateTime.Now
                };

                HandleWaveItemWithConcurrency(waveItem);
            } else {
                SendJsonResponse(context, 400, "no_audio_data");
            }
        }
        catch (Exception ex) {
            Debug.LogError($"[WavePlaybackHandler] Error processing wave data: {ex.Message}");
            SendJsonResponse(context, 500, "internal_error");
        }
    }

    /// <summary>
    /// Parse WAV data and extract samples
    /// </summary>
    private bool TryParseWavData(byte[] wavData, out float[] samples, out int sampleRate) {
        samples = null;
        sampleRate = 44100;

        if (wavData.Length < 44) return false;

        try {
            using (var ms = new MemoryStream(wavData))
            using (var br = new BinaryReader(ms)) {
                // Read WAV header
                string riff = new string(br.ReadChars(4));
                if (riff != "RIFF") return false;

                br.ReadInt32(); // file size
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

                // Skip any extra format bytes
                ms.Position += fmtSize - 16;

                // Find data chunk
                string dataId = new string(br.ReadChars(4));
                while (dataId != "data" && ms.Position < ms.Length - 4) {
                    int skipSize = br.ReadInt32();
                    ms.Position += skipSize;
                    if (ms.Position + 4 > ms.Length) return false;
                    dataId = new string(br.ReadChars(4));
                }

                if (dataId != "data") return false;

                int dataSize = br.ReadInt32();
                
                // Only support 16-bit mono PCM for now
                if (audioFormat != 1 || bitsPerSample != 16 || channels != 1) {
                    Debug.LogWarning($"[WavePlaybackHandler] Unsupported format: format={audioFormat}, bits={bitsPerSample}, channels={channels}");
                    return false;
                }

                // Read PCM data
                int sampleCount = dataSize / 2;
                samples = new float[sampleCount];
                
                for (int i = 0; i < sampleCount; i++) {
                    short sample = br.ReadInt16();
                    samples[i] = sample / 32768f; // Convert to -1.0 to 1.0 range
                }

                return true;
            }
        }
        catch (Exception ex) {
            Debug.LogError($"[WavePlaybackHandler] Error parsing WAV data: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Play audio clip directly without streaming
    /// </summary>
    private void PlayAudioClipDirectly(float[] samples, int sampleRate) {
        // Stop any current playback
        StopAudioPlayback();

        // Update avatar head position
        UpdateAvatarHeadPosition();

        // Create AudioClip from samples
        AudioClip clip = AudioClip.Create("WavePlayback", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);

        // Play through spatial audio source
        if (spatialAudioSource != null) {
            spatialAudioSource.clip = clip;
            spatialAudioSource.volume = wavePlaybackVolume;
            
            // Position at avatar head if available
            if (avatarHeadTransform != null) {
                spatialAudioSource.transform.position = avatarHeadTransform.position;
                Debug.Log($"[WavePlaybackHandler] Positioned audio at avatar head: {avatarHeadTransform.position}");
            } else {
                spatialAudioSource.transform.position = Vector3.zero;
                Debug.Log("[WavePlaybackHandler] Avatar head not found, positioning audio at origin");
            }
            
            spatialAudioSource.Play();
            AttachLipSyncCapture(spatialAudioSource);
            
            isReceivingAudio = true;
            Debug.Log($"[WavePlaybackHandler] Playing audio clip: {samples.Length} samples at {sampleRate}Hz, duration: {clip.length:F2}s");
            
            // Start coroutine to track playback completion
            StartCoroutine(TrackPlaybackCompletion(clip.length));
        }
    }

    /// <summary>
    /// Track when playback completes
    /// </summary>
    private IEnumerator TrackPlaybackCompletion(float duration) {
        yield return new WaitForSeconds(duration + 0.1f); // Add small buffer
        
        if (spatialAudioSource != null && !spatialAudioSource.isPlaying) {
            isReceivingAudio = false;
            Debug.Log("[WavePlaybackHandler] Audio playback completed");
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

        DebugLogger.LogVerbose($"[WavePlaybackHandler] Enqueued {samples.Length} samples, buffer size: {audioBuffer.Count}");
    }

    /// <summary>
    /// Initialize spatial audio source for avatar head positioning
    /// </summary>
    private void InitializeSpatialAudioSource() {
        if (spatialAudioSource == null) {
            GameObject spatialAudioGO = new GameObject("WavePlayback_SpatialAudioSource");
            spatialAudioGO.transform.SetParent(transform);
            spatialAudioSource = spatialAudioGO.AddComponent<AudioSource>();
            
            // Configure for 3D spatial audio
            spatialAudioSource.spatialBlend = 1.0f; // Full 3D
            spatialAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            spatialAudioSource.minDistance = 1f;
            spatialAudioSource.maxDistance = 10f;
            spatialAudioSource.volume = wavePlaybackVolume;
            spatialAudioSource.playOnAwake = false;
            spatialAudioSource.loop = false;
            
            Debug.Log("[WavePlaybackHandler] Spatial audio source initialized");
        }
        
        // Try to find avatar head position
        UpdateAvatarHeadPosition();
    }

    /// <summary>
    /// Update avatar head position for spatial audio
    /// </summary>
    private void UpdateAvatarHeadPosition() {
        if (vrmLoader != null && vrmLoader.LoadedModel != null) {
            // Try to get head bone from the loaded VRM model
            Transform headBone = GetHeadBone(vrmLoader.LoadedModel);
            if (headBone != null) {
                avatarHeadTransform = headBone;
                if (spatialAudioSource != null) {
                    spatialAudioSource.transform.position = avatarHeadTransform.position;
                    Debug.Log($"[WavePlaybackHandler] Avatar head position found and set: {avatarHeadTransform.position}");
                }
            } else {
                Debug.LogWarning("[WavePlaybackHandler] Could not find avatar head bone");
            }
        }
    }

    /// <summary>
    /// Get head bone from VRM model (similar to VRMLoader implementation)
    /// </summary>
    private Transform GetHeadBone(GameObject model) {
        Animator animator = model.GetComponent<Animator>();
        if (animator != null && animator.isHuman) {
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null) return headBone;
        }
        return FindNodeByName(model.transform, new string[] { "Head", "J_Bip_C_Head" });
    }

    /// <summary>
    /// Find node by name (similar to VRMLoader implementation)
    /// </summary>
    private Transform FindNodeByName(Transform root, string[] keywords) {
        foreach (Transform child in root) {
            string name = child.name.ToLowerInvariant();
            foreach (string kw in keywords) {
                if (name.Contains(kw.ToLowerInvariant())) {
                    return child;
                }
            }
            // Recursive search
            var hit = FindNodeByName(child, keywords);
            if (hit != null) return hit;
        }
        return null;
    }

    private void StartAudioPlayback() {
        if (isReceivingAudio) return;

        isReceivingAudio = true;

        // Update avatar head position before starting playback
        UpdateAvatarHeadPosition();

        // Use spatial audio source only for better control and to avoid conflicts
        if (spatialAudioSource != null) {
            spatialAudioSource.clip = streamingClip;
            spatialAudioSource.volume = wavePlaybackVolume;
            
            // Position at avatar head if available, otherwise at origin
            if (avatarHeadTransform != null) {
                spatialAudioSource.transform.position = avatarHeadTransform.position;
                Debug.Log($"[WavePlaybackHandler] Positioned audio at avatar head: {avatarHeadTransform.position}");
            } else {
                spatialAudioSource.transform.position = Vector3.zero;
                Debug.Log("[WavePlaybackHandler] Avatar head not found, positioning audio at origin");
            }
            
            spatialAudioSource.Play();
            AttachLipSyncCapture(spatialAudioSource);
            Debug.Log("[WavePlaybackHandler] Started spatial audio playback");
        }

        // Start FFT analysis if available
        if (fftAnalysisChannel != null) {
            // FFT analysis will automatically pick up the audio from the dedicated channel
        }

        Debug.Log("[WavePlaybackHandler] Started audio playback");
    }

    private void AttachLipSyncCapture(AudioSource audioSource) {
        try {
            if (audioSource == null) return;

            var captureType = System.Type.GetType("AudioSourceLipSyncCapture");
            if (captureType == null) return;

            var existing = audioSource.GetComponent(captureType);
            if (existing != null) {
                Destroy(existing);
            }

            audioSource.gameObject.AddComponent(captureType);
        }
        catch (System.Exception ex) {
            Debug.LogWarning($"[WavePlaybackHandler] Failed to attach LipSyncCapture: {ex.Message}");
        }
    }

    private void StopAudioPlayback() {
        if (!isReceivingAudio) return;

        isReceivingAudio = false;

        // Stop playback
        if (audioChannelManager != null) {
            audioChannelManager.StopAllChannels();
        }

        // Stop spatial audio source
        if (spatialAudioSource != null && spatialAudioSource.isPlaying) {
            spatialAudioSource.Stop();
        }

        // Clear buffer
        lock (audioBuffer) {
            audioBuffer.Clear();
        }

        Debug.Log("[WavePlaybackHandler] Stopped audio playback");
    }

    /// <summary>
    /// Update method to continuously track avatar head position
    /// </summary>
    private void Update() {
        // Update spatial audio source position to follow avatar head
        if (spatialAudioSource != null && avatarHeadTransform != null) {
            spatialAudioSource.transform.position = avatarHeadTransform.position;
        }
        
        // Check if VRM model has been loaded/changed
        if (vrmLoader != null && vrmLoader.LoadedModel != null && avatarHeadTransform == null) {
            UpdateAvatarHeadPosition();
        }
    }

    /// <summary>
    /// Set wave playback volume (0.0-3.0, where 1.0 = normal volume, 2.0 = double volume)
    /// </summary>
    public void SetWavePlaybackVolume(float volume) {
        // Allow amplification up to 3x, but clamp to reasonable range
        wavePlaybackVolume = Mathf.Clamp(volume, 0.0f, 3.0f);
        
        // Update volume on spatial audio source only (avoid conflicts with other systems)
        if (spatialAudioSource != null) {
            spatialAudioSource.volume = wavePlaybackVolume;
        }
        
        Debug.Log($"[WavePlaybackHandler] Wave playback volume set to: {wavePlaybackVolume:F2}");
    }

    /// <summary>
    /// Get current wave playback volume
    /// </summary>
    public float GetWavePlaybackVolume() {
        return wavePlaybackVolume;
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

    /// <summary>
    /// Handle wave item based on concurrency mode
    /// </summary>
    private void HandleWaveItemWithConcurrency(WavePlaybackItem waveItem) {
        var config = ServerConfig.Instance;
        string concurrency = config?.wavePlaybackConcurrency ?? "interrupt";
        
        Debug.Log($"[WavePlaybackHandler] Processing audio {waveItem.audioId} with concurrency mode: {concurrency}");
        
        lock (queueLock) {
            switch (concurrency.ToLower()) {
                case "queue":
                    HandleQueueMode(waveItem);
                    break;
                case "reject":
                    HandleRejectMode(waveItem);
                    break;
                case "interrupt":
                default:
                    HandleInterruptMode(waveItem);
                    break;
            }
        }
    }
    
    /// <summary>
    /// Handle interrupt mode - always stop current and play new
    /// </summary>
    private void HandleInterruptMode(WavePlaybackItem waveItem) {
        string prevId = null;
        if (spatialAudioSource != null && spatialAudioSource.isPlaying) {
            prevId = "current_audio"; // We don't track IDs in interrupt mode currently
            StopAudioPlayback();
        }
        
        PlayWaveItem(waveItem);
        
        if (prevId != null) {
            SendJsonResponse(waveItem.context, 200, "interrupted", waveItem.audioId, prevId);
        } else {
            SendJsonResponse(waveItem.context, 200, "ok", waveItem.audioId);
        }
    }
    
    /// <summary>
    /// Handle queue mode - queue if playing, play immediately if not
    /// </summary>
    private void HandleQueueMode(WavePlaybackItem waveItem) {
        if (spatialAudioSource != null && spatialAudioSource.isPlaying) {
            // Check queue size limit
            if (waveQueue.Count >= MAX_QUEUE_SIZE) {
                // Remove oldest item
                var oldestItem = waveQueue.Dequeue();
                SendJsonResponse(oldestItem.context, 410, "queue_full_dropped", oldestItem.audioId);
                Debug.LogWarning($"[WavePlaybackHandler] Queue full, dropped oldest item {oldestItem.audioId}");
            }
            
            waveQueue.Enqueue(waveItem);
            SendJsonResponse(waveItem.context, 200, "queued", waveItem.audioId);
            Debug.Log($"[WavePlaybackHandler] Queued item {waveItem.audioId}, queue size: {waveQueue.Count}");
        } else {
            // Not playing, play immediately
            PlayWaveItem(waveItem);
            SendJsonResponse(waveItem.context, 200, "ok", waveItem.audioId);
        }
    }
    
    /// <summary>
    /// Handle reject mode - reject if playing, play if not
    /// </summary>
    private void HandleRejectMode(WavePlaybackItem waveItem) {
        if (spatialAudioSource != null && spatialAudioSource.isPlaying) {
            SendJsonResponse(waveItem.context, 409, "busy");
        } else {
            PlayWaveItem(waveItem);
            SendJsonResponse(waveItem.context, 200, "ok", waveItem.audioId);
        }
    }
    
    /// <summary>
    /// Play a wave item with spatial audio support
    /// </summary>
    private void PlayWaveItem(WavePlaybackItem waveItem) {
        if (isProcessingQueue) {
            Debug.LogWarning($"[WavePlaybackHandler] Already processing queue, skipping {waveItem.audioId}");
            return;
        }
        
        isProcessingQueue = true;
        
        try {
            // Parse WAV data
            if (!TryParseWavData(waveItem.audioData, out float[] samples, out int parsedSampleRate)) {
                Debug.LogError($"[WavePlaybackHandler] Failed to parse WAV data for {waveItem.audioId}");
                SendJsonResponse(waveItem.context, 422, "invalid_wav", waveItem.audioId);
                return;
            }
            
            // Update avatar head position
            UpdateAvatarHeadPosition();
            
            // Create AudioClip from samples
            AudioClip clip = AudioClip.Create($"WavePlayback_{waveItem.audioId}", samples.Length, 1, parsedSampleRate, false);
            clip.SetData(samples, 0);
            
            // Configure spatial audio
            var config = ServerConfig.Instance;
            bool spatial = waveItem.spatialOverride ?? (config?.waveSpatializationEnabled ?? true);
            float baseVolume = config?.wavePlaybackVolume ?? 1.0f;
            
            if (spatialAudioSource != null) {
                spatialAudioSource.clip = clip;
                spatialAudioSource.volume = baseVolume * waveItem.volume;
                spatialAudioSource.spatialBlend = spatial ? 1.0f : 0.0f;
                
                // Position at avatar head if available
                if (avatarHeadTransform != null) {
                    spatialAudioSource.transform.position = avatarHeadTransform.position;
                    Debug.Log($"[WavePlaybackHandler] Positioned audio at avatar head: {avatarHeadTransform.position}");
                } else {
                    spatialAudioSource.transform.position = Vector3.zero;
                    Debug.Log("[WavePlaybackHandler] Avatar head not found, positioning audio at origin");
                }
                
                spatialAudioSource.Play();
                AttachLipSyncCapture(spatialAudioSource);
                
                isReceivingAudio = true;
                Debug.Log($"[WavePlaybackHandler] Playing audio {waveItem.audioId}: {samples.Length} samples at {parsedSampleRate}Hz, duration: {clip.length:F2}s");
                
                // Start coroutine to track playback completion and process queue
                StartCoroutine(TrackPlaybackCompletionWithQueue(clip.length, waveItem.audioId));
            }
        } catch (Exception ex) {
            Debug.LogError($"[WavePlaybackHandler] Error playing wave item {waveItem.audioId}: {ex.Message}");
            SendJsonResponse(waveItem.context, 500, "playback_error", waveItem.audioId);
        } finally {
            isProcessingQueue = false;
        }
    }
    
    /// <summary>
    /// Track playback completion and process next queue item
    /// </summary>
    private IEnumerator TrackPlaybackCompletionWithQueue(float duration, string audioId) {
        yield return new WaitForSeconds(duration + 0.1f); // Add small buffer
        
        if (spatialAudioSource != null && !spatialAudioSource.isPlaying) {
            isReceivingAudio = false;
            Debug.Log($"[WavePlaybackHandler] Audio playback completed for {audioId}");
            
            // Process next item in queue
            ProcessNextQueueItem();
        }
    }
    
    /// <summary>
    /// Process next item in queue if available
    /// </summary>
    private void ProcessNextQueueItem() {
        lock (queueLock) {
            if (waveQueue.Count > 0) {
                var nextItem = waveQueue.Dequeue();
                Debug.Log($"[WavePlaybackHandler] Processing next queued item {nextItem.audioId}, remaining queue: {waveQueue.Count}");
                
                // Play next item
                PlayWaveItem(nextItem);
            }
        }
    }
    
    /// <summary>
    /// Send JSON response to HTTP context
    /// </summary>
    private void SendJsonResponse(HttpListenerContext context, int statusCode, string message, string id = null, string prevId = null) {
        if (context == null) {
            Debug.LogWarning("[WavePlaybackHandler] Cannot send response: HttpListenerContext is null");
            return;
        }
        
        try {
            string body;
            if (prevId != null)
                body = $"{{\"status\":\"{message}\",\"prev_id\":\"{prevId}\",\"id\":\"{id}\"}}";
            else if (id != null)
                body = $"{{\"status\":\"{message}\",\"id\":\"{id}\"}}";
            else
                body = $"{{\"status\":\"{message}\"}}";
            
            var buffer = System.Text.Encoding.UTF8.GetBytes(body);
            
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            
            using (var stream = context.Response.OutputStream) {
                stream.Write(buffer, 0, buffer.Length);
            }
        } catch (Exception ex) {
            Debug.LogWarning($"[WavePlaybackHandler] SendJsonResponse transport failure: {ex.Message}");
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
        
        // Unsubscribe from VRM load events
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnVRMLoadComplete;
        }
        
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

[System.Serializable]
public class WavePlaybackItem {
    public byte[] audioData;
    public HttpListenerContext context;
    public string audioId;
    public float volume;
    public bool? spatialOverride;
    public DateTime enqueuedTime;
}
