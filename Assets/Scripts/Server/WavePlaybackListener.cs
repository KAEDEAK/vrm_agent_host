using System;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

public class WavePlaybackListener : MonoBehaviour {
    public static WavePlaybackListener Instance { get; private set; }

    private HttpListener listener;
    private Thread listenThread;
    private volatile bool stopping = false;
    private AudioSource audioSource;

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
    }

    private void HandleContext(HttpListenerContext context) {
        if (context.Request.HttpMethod != "POST") {
            SendJson(context, 405, "method_not_allowed");
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
        byte[] data;
        using (var ms = new MemoryStream()) {
            context.Request.InputStream.CopyTo(ms);
            data = ms.ToArray();
        }
        MainThreadInvoker.Invoke(() => PlayWave(data, context));
    }

    private void PlayWave(byte[] data, HttpListenerContext context) {
        if (audioSource == null) {
            var go = new GameObject("WavePlaybackSource");
            audioSource = go.AddComponent<AudioSource>();
        }
        try {
            if (!TryParseWav(data, out float[] samples, out int sampleRate)) {
                SendJson(context, 422, "invalid_wav");
                return;
            }
            var clip = AudioClip.Create("wave", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.spatialBlend = ServerConfig.Instance.waveSpatializationEnabled ? 1f : 0f;
            audioSource.volume = ServerConfig.Instance.wavePlaybackVolume;
            audioSource.clip = clip;
            audioSource.Play();
            SendJson(context, 200, "ok");
        } catch (Exception e) {
            Debug.LogError($"[Wave] playback error: {e.Message}");
            SendJson(context, 500, "internal_error");
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

    private void SendJson(HttpListenerContext ctx, int status, string msg) {
        var body = $"{{\"status\":\"{msg}\"}}";
        var buf = System.Text.Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = buf.Length;
        using (var s = ctx.Response.OutputStream) {
            s.Write(buf, 0, buf.Length);
        }
    }
}
