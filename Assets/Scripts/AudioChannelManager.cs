using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System;

public class AudioChannelManager : MonoBehaviour {
    public static AudioChannelManager Instance { get; private set; }

    [Header("Audio Mixer Configuration")]
    public AudioMixer audioMixer;
    
    [Header("Audio Sources")]
    public AudioSource masterAudioSource;
    public AudioSource fftAnalysisAudioSource;
    public AudioSource monitorAudioSource;

    [Header("Channel Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Range(0f, 1f)]
    public float fftAnalysisVolume = 1f;
    [Range(0f, 1f)]
    public float monitorVolume = 0f; // Usually muted for monitoring

    private AudioMixerGroup masterGroup;
    private AudioMixerGroup fftAnalysisGroup;
    private AudioMixerGroup monitorGroup;

    // Events for channel state changes
    public event Action<AudioClip> OnAudioClipChanged;
    public event Action<bool> OnPlaybackStateChanged;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
            // エディタ環境以外でのみDontDestroyOnLoadを適用
            #if !UNITY_EDITOR
            DontDestroyOnLoad(gameObject);
            #endif
            InitializeChannels();
        } else {
            Destroy(gameObject);
        }
    }

    private void InitializeChannels() {
        // Load AudioMixer if not assigned
        if (audioMixer == null) {
            audioMixer = Resources.Load<AudioMixer>("AudioMixer");
            if (audioMixer == null) {
                Debug.LogWarning("[AudioChannelManager] AudioMixer not found in Resources. Creating fallback audio setup...");
                CreateFallbackAudioSetup();
                return;
            }
        }

        // Get mixer groups
        AudioMixerGroup[] groups = audioMixer.FindMatchingGroups("Master");
        if (groups.Length > 0) masterGroup = groups[0];

        groups = audioMixer.FindMatchingGroups("FFT Analysis");
        if (groups.Length > 0) fftAnalysisGroup = groups[0];

        groups = audioMixer.FindMatchingGroups("Monitor");
        if (groups.Length > 0) monitorGroup = groups[0];

        // Create AudioSources if not assigned
        CreateAudioSourcesIfNeeded();

        // Assign mixer groups to audio sources
        AssignMixerGroups();

        // Set initial volumes
        UpdateVolumes();

        Debug.Log("[AudioChannelManager] Initialized with mixer groups - Master, FFT Analysis, Monitor");
    }

    /// <summary>
    /// AudioMixerが見つからない場合のフォールバック処理
    /// </summary>
    private void CreateFallbackAudioSetup() {
        Debug.Log("[AudioChannelManager] Setting up fallback audio configuration without AudioMixer");
        
        // Create AudioSources without mixer groups
        CreateAudioSourcesIfNeeded();
        
        // Set fallback volumes directly on AudioSources
        if (masterAudioSource != null) {
            masterAudioSource.volume = masterVolume;
            masterAudioSource.mute = false;
        }
        
        if (fftAnalysisAudioSource != null) {
            fftAnalysisAudioSource.volume = fftAnalysisVolume;
            fftAnalysisAudioSource.mute = true; // Muted for analysis only
        }
        
        if (monitorAudioSource != null) {
            monitorAudioSource.volume = monitorVolume;
            monitorAudioSource.mute = true; // Usually muted
        }
        
        Debug.Log("[AudioChannelManager] Fallback audio setup completed - AudioSources created without mixer");
    }

    private void CreateAudioSourcesIfNeeded() {
        if (masterAudioSource == null) {
            GameObject masterGO = new GameObject("MasterAudioSource");
            masterGO.transform.SetParent(transform);
            masterAudioSource = masterGO.AddComponent<AudioSource>();
            masterAudioSource.playOnAwake = false;
            masterAudioSource.loop = false;
            masterAudioSource.mute = false; // Master should not be muted for audio output
        }

        if (fftAnalysisAudioSource == null) {
            GameObject fftGO = new GameObject("FFTAnalysisAudioSource");
            fftGO.transform.SetParent(transform);
            fftAnalysisAudioSource = fftGO.AddComponent<AudioSource>();
            fftAnalysisAudioSource.playOnAwake = false;
            fftAnalysisAudioSource.loop = false;
            fftAnalysisAudioSource.mute = true; // Muted for analysis only
        }

        if (monitorAudioSource == null) {
            GameObject monitorGO = new GameObject("MonitorAudioSource");
            monitorGO.transform.SetParent(transform);
            monitorAudioSource = monitorGO.AddComponent<AudioSource>();
            monitorAudioSource.playOnAwake = false;
            monitorAudioSource.loop = false;
            monitorAudioSource.mute = true; // Usually muted
        }
    }

    private void AssignMixerGroups() {
        if (masterAudioSource != null && masterGroup != null) {
            masterAudioSource.outputAudioMixerGroup = masterGroup;
        }

        if (fftAnalysisAudioSource != null && fftAnalysisGroup != null) {
            fftAnalysisAudioSource.outputAudioMixerGroup = fftAnalysisGroup;
        }

        if (monitorAudioSource != null && monitorGroup != null) {
            monitorAudioSource.outputAudioMixerGroup = monitorGroup;
        }
    }

    private void UpdateVolumes() {
        if (audioMixer == null) return;

        // Convert linear volume to decibel (Unity AudioMixer uses dB)
        float masterDB = masterVolume > 0 ? Mathf.Log10(masterVolume) * 20f : -80f;
        float fftDB = fftAnalysisVolume > 0 ? Mathf.Log10(fftAnalysisVolume) * 20f : -80f;
        float monitorDB = monitorVolume > 0 ? Mathf.Log10(monitorVolume) * 20f : -80f;

        audioMixer.SetFloat("MasterVolume", masterDB);
        audioMixer.SetFloat("FFTAnalysisVolume", fftDB);
        audioMixer.SetFloat("MonitorVolume", monitorDB);
    }

    /// <summary>
    /// Play audio clip on all channels simultaneously
    /// </summary>
    public void PlayAudioClip(AudioClip clip, float volume = 1f, bool spatial = false) {
        if (clip == null) {
            Debug.LogWarning("[AudioChannelManager] Attempted to play null AudioClip");
            return;
        }

        // Stop any currently playing audio
        StopAllChannels();

        // Configure spatial settings
        float spatialBlend = spatial ? 1f : 0f;

        // Set clip and volume for all sources
        if (masterAudioSource != null) {
            masterAudioSource.clip = clip;
            masterAudioSource.volume = volume;
            masterAudioSource.spatialBlend = spatialBlend;
            masterAudioSource.Play();
        }

        if (fftAnalysisAudioSource != null) {
            fftAnalysisAudioSource.clip = clip;
            fftAnalysisAudioSource.volume = volume;
            fftAnalysisAudioSource.spatialBlend = spatialBlend;
            fftAnalysisAudioSource.Play();
        }

        if (monitorAudioSource != null) {
            monitorAudioSource.clip = clip;
            monitorAudioSource.volume = volume;
            monitorAudioSource.spatialBlend = spatialBlend;
            monitorAudioSource.Play();
        }

        OnAudioClipChanged?.Invoke(clip);
        OnPlaybackStateChanged?.Invoke(true);

        Debug.Log($"[AudioChannelManager] Playing audio clip: {clip.name} on all channels");
    }

    /// <summary>
    /// Stop all audio channels
    /// </summary>
    public void StopAllChannels() {
        if (masterAudioSource != null && masterAudioSource.isPlaying) {
            masterAudioSource.Stop();
        }

        if (fftAnalysisAudioSource != null && fftAnalysisAudioSource.isPlaying) {
            fftAnalysisAudioSource.Stop();
        }

        if (monitorAudioSource != null && monitorAudioSource.isPlaying) {
            monitorAudioSource.Stop();
        }

        OnPlaybackStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Check if any channel is currently playing
    /// </summary>
    public bool IsPlaying() {
        return (masterAudioSource != null && masterAudioSource.isPlaying) ||
               (fftAnalysisAudioSource != null && fftAnalysisAudioSource.isPlaying) ||
               (monitorAudioSource != null && monitorAudioSource.isPlaying);
    }

    /// <summary>
    /// Get the current playback time
    /// </summary>
    public float GetPlaybackTime() {
        if (masterAudioSource != null && masterAudioSource.isPlaying) {
            return masterAudioSource.time;
        }
        return 0f;
    }

    /// <summary>
    /// Set master volume (0-1)
    /// </summary>
    public void SetMasterVolume(float volume) {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Set FFT analysis volume (0-1)
    /// </summary>
    public void SetFFTAnalysisVolume(float volume) {
        fftAnalysisVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Set monitor volume (0-1)
    /// </summary>
    public void SetMonitorVolume(float volume) {
        monitorVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Toggle monitor channel mute
    /// </summary>
    public void SetMonitorMute(bool mute) {
        if (monitorAudioSource != null) {
            monitorAudioSource.mute = mute;
        }
    }

    /// <summary>
    /// Get the FFT analysis AudioSource for spectrum analysis
    /// </summary>
    public AudioSource GetFFTAnalysisAudioSource() {
        return fftAnalysisAudioSource;
    }

    /// <summary>
    /// Get the master AudioSource
    /// </summary>
    public AudioSource GetMasterAudioSource() {
        return masterAudioSource;
    }

    /// <summary>
    /// Get channel status information
    /// </summary>
    public AudioChannelStatus GetChannelStatus() {
        return new AudioChannelStatus {
            isPlaying = IsPlaying(),
            masterVolume = masterVolume,
            fftAnalysisVolume = fftAnalysisVolume,
            monitorVolume = monitorVolume,
            currentClip = masterAudioSource?.clip?.name ?? "None",
            playbackTime = GetPlaybackTime()
        };
    }

    private void OnValidate() {
        // Update volumes when values change in inspector
        if (Application.isPlaying) {
            UpdateVolumes();
        }
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }
}

[System.Serializable]
public class AudioChannelStatus {
    public bool isPlaying;
    public float masterVolume;
    public float fftAnalysisVolume;
    public float monitorVolume;
    public string currentClip;
    public float playbackTime;
}
