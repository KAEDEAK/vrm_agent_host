using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FileControlEntry {
    public string key;
    public bool listing;
}

[Serializable]
public class AnimationOverride {
    public string category;
    public string key;
    public int    value;
}

[Serializable]
public class WindowPositionData {
    public int x = 100, y = 100, width = 1280, height = 720;
}

[Serializable]
public class WindowConfigData {
    public bool transparent = false;
    public string transparentColor = "#FF00FF";
    public bool allowDragObjects = false;
    public bool saveWindowBounds = false;
    public bool stayOnTop = false;
    public bool pointerEventsNone = false;
    public bool borderless = true;
    public bool magic = false;
    public WindowPositionData position = new WindowPositionData();
    public int monitorIndex = 0;
}

[Serializable]
public class CameraConfigData {
    public bool orthographic = false;
    public float orthographicSize = 0.4f;
    public int antiAliasing = 4;
}

[Serializable]
public class BandRangeEntry {
    public string key;
    public float[] range;
}

[Serializable]
public class LipSyncConfig {
    public List<BandRangeEntry> bandRanges = new List<BandRangeEntry>();
}

[Serializable]
public class ServerConfigData {
    public int httpPort = 34560;
    public int httpsPort = 34561;
    public bool useHttp = true;
    public bool useHttps = false;
    public bool listenLocalhostOnly = true;
    public bool autoPrepareSeamless = false;
    public bool vsync = true;
    public int targetFramerate = 60;
    public List<string> allowedRemoteIPs = new List<string> { "127.0.0.1", "::1" };

    // --- Wave Playback ---
    public bool wavePlaybackEnabled = false;
    public int  wavePlaybackPort = 50800;
    public float wavePlaybackVolume = 1.0f;
    public bool waveSpatializationEnabled = true;
    public int  wavePayloadMaxBytes = 5000000;
    public bool waveListenerAutoRestart = true;
    public int  lipSyncOffsetMs = 0;
    public string wavePlaybackConcurrency = "interrupt";

    public List<FileControlEntry> fileControl = new List<FileControlEntry>();
    public List<AnimationOverride> animations = new List<AnimationOverride>();
    public WindowConfigData window = new WindowConfigData();
    public CameraConfigData camera = new CameraConfigData();
    public LipSyncConfig lipSync = new LipSyncConfig();

    public List<string> outputFilters = new List<string>();
}
