using UnityEngine;
using UniVRM10;

public class LipSyncFFTProcessor : MonoBehaviour {
    public AudioSource targetAudioSource;
    private float[] spectrum = new float[256];

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
        var expr = VRMLoader.Instance?.VrmInstance?.Runtime?.Expression;
        if (expr != null) {
            expr.SetWeight(ExpressionKey.Aa, w);
        }
    }

    public void ResetMouth() {
        var expr = VRMLoader.Instance?.VrmInstance?.Runtime?.Expression;
        if (expr != null) expr.SetWeight(ExpressionKey.Aa, 0f);
    }
}
