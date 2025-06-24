using UnityEngine;
using UniVRM10;

public class LipSyncFFTProcessor : MonoBehaviour {
    public AudioSource targetAudioSource;
    private float[] spectrum = new float[256];
    private VRMLoader vrmLoader;
    private Vrm10RuntimeExpression expression;

    private void Start() {
        vrmLoader = FindAnyObjectByType<VRMLoader>();
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete += OnModelLoaded;
            // 既にモデルがロードされている場合
            if (vrmLoader.VrmInstance?.Runtime?.Expression != null) {
                expression = vrmLoader.VrmInstance.Runtime.Expression;
            }
        }
    }

    private void OnDestroy() {
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnModelLoaded;
        }
    }

    private void OnModelLoaded(GameObject vrmModel) {
        if (vrmLoader?.VrmInstance?.Runtime?.Expression != null) {
            expression = vrmLoader.VrmInstance.Runtime.Expression;
            Debug.Log("[LipSyncFFTProcessor] VRM expression system connected");
        }
    }

    void Update() {
        if (targetAudioSource != null && targetAudioSource.isPlaying) {
            targetAudioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            float energy = ComputeVolume(spectrum);
            ApplyMouthExpression(energy);
        }
    }

    private float ComputeVolume(float[] spectrum) {
        float sum = 0f;
        for (int i = 4; i < 40; i++) sum += spectrum[i]; // 約300Hz〜3000Hz
        return Mathf.Clamp01(sum * 10f);
    }

    private void ApplyMouthExpression(float w) {
        if (expression != null) {
            expression.SetWeight(ExpressionKey.Aa, w);
        }
    }

    public void ResetMouth() {
        if (expression != null) {
            expression.SetWeight(ExpressionKey.Aa, 0f);
        }
    }
}
