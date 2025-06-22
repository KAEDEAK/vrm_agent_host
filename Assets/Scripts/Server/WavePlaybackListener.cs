using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using UnityEngine;

public class WavePlaybackListener : MonoBehaviour {
    public static WavePlaybackListener Instance { get; private set; }

    private HttpListener listener;
    private Thread listenThread;
    private volatile bool stopping = false;
    private AudioSource audioSource;
    private Coroutine playbackRoutine;
    private string currentAudioId;
    private float playStartTime;
    private int restartAttempts;
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

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start() {
        if (ServerConfig.Instance.wavePlaybackEnabled) {
            StartListener();
        }
    }

    private void OnDestroy() {
        StopListener();
        if (Instance == this) Instance = null;
    }

    public bool IsRunning => listener != null && listener.IsListening;

    public void StartListener() {
        if (IsRunning) return;
        stopping = false;
        restartAttempts = 0;
        if (lipSync == null) lipSync = GameObject.FindObjectOfType<AudioLipSync>();
        int port = ServerConfig.Instance.wavePlaybackPort;
        listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{port}/");
        try {
            listener.Start();
        } catch (Exception e) {
            Debug.LogError($"[Wave] failed to start listener: {e.Message}");
            listener = null;
            return;
        }
        listenThread = new Thread(ListenLoop);
        listenThread.Start();
        Debug.Log($"[Wave] listener started on port {port}");
    }

    public void StopListener() {
        stopping = true;
        try {
            listener?.Stop();
            listener?.Close();
        } catch {}
        listener = null;
        if (listenThread != null && listenThread.IsAlive) {
            listenThread.Join();
            listenThread = null;
        }
        Debug.Log("[Wave] listener stopped");
    }

    private void ListenLoop() {
        while (!stopping && listener != null && listener.IsListening) {
            HttpListenerContext ctx = null;
            try {
                ctx = listener.GetContext();
            } catch (Exception) {
                if (!stopping)
                    Debug.LogWarning("[Wave] listener exception");
                break;
            }
            if (ctx != null) {
                HandleContext(ctx);
            }
        }
        if (!stopping && ServerConfig.Instance.waveListenerAutoRestart && restartAttempts < 5) {
            restartAttempts++;
            MainThreadInvoker.Invoke(() => StartCoroutine(RestartAfterDelay()));
        }
    }

    private IEnumerator RestartAfterDelay() {
        yield return new WaitForSeconds(1f);
        if (!stopping) StartListener();
    }

    private void HandleContext(HttpListenerContext context) {
        if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/ping") {
            SendJson(context, 200, "ok");
            return;
        }
        if (context.Request.HttpMethod != "POST") {
            SendJson(context, 405, "method_not_allowed");
            return;
        }

        string path = context.Request.Url.AbsolutePath;
        if (!(string.IsNullOrEmpty(path) || path == "/" || path == "/waveplay")) {
            SendJson(context, 404, "not_found");
            return;
        }

        if (!context.Request.ContentType?.StartsWith("audio/wav") ?? true) {
            SendJson(context, 415, "unsupported_media_type");
            return;
        }

        long len = context.Request.ContentLength64;
        int max = ServerConfig.Instance.wavePayloadMaxBytes;
        if (len <= 0 || len > max) {
            SendJson(context, 413, "payload_too_large");
            return;
        }

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

        byte[] data;
        using (var ms = new MemoryStream()) {
            context.Request.InputStream.CopyTo(ms);
            data = ms.ToArray();
        }

        MainThreadInvoker.Invoke(() => EnqueueWave(data, context, audioId, volume, spatialOverride));
    }

    private void EnqueueWave(byte[] data, HttpListenerContext context, string audioId, float headerVolume, bool? spatialOverride) {
        string mode = ServerConfig.Instance.wavePlaybackConcurrency ?? "interrupt";
        if (lastConcurrency != mode) {
            if (lastConcurrency == "queue" && mode != "queue") waveQueue.Clear();
            lastConcurrency = mode;
        }

        var item = new WaveItem{ data = data, context = context, id = audioId, volume = headerVolume, spatial = spatialOverride };

        if (mode == "queue") {
            if (audioSource != null && audioSource.isPlaying) {
                waveQueue.Enqueue(item);
                SendJson(context, 200, "queued", audioId);
                return;
            }
            PlayWave(item);
            return;
        }
        if (mode == "reject" && audioSource != null && audioSource.isPlaying) {
            SendJson(context, 409, "busy");
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
                SendJson(context, 422, "invalid_wav");
                return;
            }
            var clip = AudioClip.Create("wave", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            bool spatial = spatialOverride ?? ServerConfig.Instance.waveSpatializationEnabled;
            audioSource.spatialBlend = spatial ? 1f : 0f;
            float baseVol = ServerConfig.Instance.wavePlaybackVolume;
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
                SendJson(context, 200, "interrupted", audioId, prevId);
            else
                SendJson(context, 200, "ok", audioId);
        } catch (Exception e) {
            Debug.LogError($"[Wave] playback error: {e.Message}");
            SendJson(context, 500, "internal_error");
        }
    }

    private IEnumerator PlaybackCoroutine(float[] samples, int sampleRate, string audioId) {
        int step = Mathf.Max(1, sampleRate / 100); // 10ms
        float offset = ServerConfig.Instance.lipSyncOffsetMs / 1000f;
        float next = Time.time + offset;
        for (int i = 0; i < samples.Length; i += step) {
            float sum = 0f;
            int count = 0;
            for (int j = 0; j < step && i + j < samples.Length; j++) { sum += samples[i + j] * samples[i + j]; count++; }
            float rms = Mathf.Sqrt(sum / count);
            lipSync?.FeedWaveRms(rms);
            float delay = next - Time.time;
            if (delay > 0f) yield return new WaitForSeconds(delay); else yield return null;
            next += 0.01f;
        }
        lipSync?.FeedWaveRms(0f);
        Telemetry.LogEvent("wave_complete", new System.Collections.Generic.Dictionary<string, object>{{"id", audioId},{"duration_ms", (int)((Time.time - playStartTime)*1000)}});
        currentAudioId = null;
        playbackRoutine = null;
        if (ServerConfig.Instance.wavePlaybackConcurrency == "queue" && waveQueue.Count > 0) {
            var next = waveQueue.Dequeue();
            PlayWave(next);
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

    private void SendJson(HttpListenerContext ctx, int status, string msg, string id = null, string prevId = null) {
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
        using (var s = ctx.Response.OutputStream) {
            s.Write(buf, 0, buf.Length);
        }
    }
}
