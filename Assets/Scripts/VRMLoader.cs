// File: Assets/Scripts/VRMLoader.cs
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UniVRM10;
using VRM;

public class VRMLoader : MonoBehaviour {
    public GameObject LoadedModel { get; private set; }
    public Vrm10Instance VrmInstance { get; private set; }

    public delegate void VRMLoadCompleteHandler(GameObject model);
    public event VRMLoadCompleteHandler OnVRMLoadComplete;

    private bool isLoading = false;

    public async Task ReloadVRMAsync(string fullPath) {
        if (isLoading) {
            Debug.LogWarning(i18nMsg.VRML_RELOADING_IGNORED);
            return;
        }

        isLoading = true;
        try {
            if (LoadedModel != null) {
                Debug.Log(i18nMsg.VRML_EXISTING_MODEL_DESTROYED);
                Destroy(LoadedModel);
                LoadedModel = null;
                VrmInstance = null;
                // ★UPDATE★ GC.Collect() を削除し、Resources.UnloadUnusedAssets() を呼び出す
                await Task.Yield();
                Resources.UnloadUnusedAssets();
            }

            Debug.Log(string.Format(i18nMsg.VRML_LOAD_START, fullPath));
            byte[] vrmData = await File.ReadAllBytesAsync(fullPath);
            await Task.Yield();

            VrmInstance = await Vrm10.LoadBytesAsync(vrmData,
                vrmMetaInformationCallback: (thumbnail, meta, vrm0Meta) =>
                {
                    // この時点では meta は UniGLTF のもの → 無視してOK
                }
            );

            if (VrmInstance?.Vrm?.Meta != null)
            {
                VrmUsagePolicy.Instance.UpdateFromMeta(VrmInstance.Vrm.Meta);

                // ▼▼▼ ログ出力！（取得フラグの中身） ▼▼▼
                Debug.Log($"【VRMLicense】性表現: {VrmUsagePolicy.Instance.IsSexualExpressionAllowed}");
                Debug.Log($"【VRMLicense】暴力表現: {VrmUsagePolicy.Instance.IsViolentExpressionAllowed}");
                Debug.Log($"【VRMLicense】再配布: {VrmUsagePolicy.Instance.IsRedistributionAllowed}");
                Debug.Log($"【VRMLicense】商用利用: {VrmUsagePolicy.Instance.IsCommercialUsageAllowed}");
            }

            LoadedModel = VrmInstance.gameObject;
            LoadedModel.name = "LoadedVRMModel";

            if (LoadedModel.GetComponent<EyeRotationOverride>() == null)
            {
                LoadedModel.AddComponent<EyeRotationOverride>();
                Debug.Log("[VRMLoader] EyeRotationOverride added to loaded model");
            }

            var lookAtHead = LoadedModel.GetComponent<VRM.VRMLookAtHead>();
            if (lookAtHead != null)
            {
                lookAtHead.UpdateType = VRM.UpdateType.LateUpdate;
                Debug.Log("[VRMLoader] VRMLookAtHead UpdateType set to LateUpdate");
            }

            EnableUpdateWhenOffscreen(LoadedModel);
            AdjustCamera(LoadedModel);

            Debug.Log(i18nMsg.VRML_LOAD_SUCCESS);
            OnVRMLoadComplete?.Invoke(LoadedModel);
        }
        catch (Exception ex) {
            Debug.LogError(string.Format(i18nMsg.VRML_LOAD_EXCEPTION, ex.Message));
        }
        finally {
            isLoading = false;
        }
    }

    private void EnableUpdateWhenOffscreen(GameObject model) {
        var skinnedMeshes = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var skinnedMesh in skinnedMeshes) {
            skinnedMesh.updateWhenOffscreen = true;
            Debug.Log(string.Format(i18nMsg.VRML_UPDATE_WHEN_OFFSCREEN_ENABLED, skinnedMesh.gameObject.name));
        }
    }

    /// <summary>
    /// 外部から安全にカメラを調整するためのラッパー関数。
    /// 内部の AdjustCamera(GameObject) 実行に失敗した場合は false を返します。
    /// </summary>
    public bool AdjustCameraFromExt() {
        if (Camera.main == null) {
            Debug.LogWarning("AdjustCameraFromExt: MainCameraが存在しません。");
            return false;
        }

        if (LoadedModel == null) {
            Debug.LogWarning("AdjustCameraFromExt: LoadedModelがnullです。");
            return false;
        }

        try {
            AdjustCamera(LoadedModel);
            return true;
        }
        catch (System.Exception ex) {
            Debug.LogError($"AdjustCamera失敗: {ex.Message}");
            return false;
        }
    }

    private void AdjustCamera(GameObject model) {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) {
            Debug.LogError(i18nMsg.VRML_MAIN_CAMERA_NOT_FOUND);
            return;
        }

        var config = ServerConfig.Instance;
        mainCamera.orthographic = config.Camera.orthographic;
        if (config.Camera.orthographic) {
            mainCamera.orthographicSize = config.Camera.orthographicSize;
        }
        else {
            mainCamera.fieldOfView = config.Camera.fieldOfView;
        }
        mainCamera.nearClipPlane = 0.02f;
        mainCamera.farClipPlane = 1000f;
        model.transform.localScale = Vector3.one;

        mainCamera.transform.position = config.Camera.position;
        if (!config.Camera.headTracking) {
            mainCamera.transform.rotation = Quaternion.Euler(config.Camera.rotation);
            Debug.Log($"[VRMLoader] Camera set by config: pos={mainCamera.transform.position}, rot={mainCamera.transform.rotation.eulerAngles}");
            return;
        }

        Transform headBone = GetHeadBone(model);
        if (headBone == null) {
            Debug.LogError(i18nMsg.VRML_HEAD_BONE_NOT_FOUND);
            return;
        }

        Vector3 facePosition = headBone.position;
        Vector3 modelForward = GetModelForward(model);
        if (IsVroidModel(model)) {
            modelForward = -model.transform.forward;
        }

        float cameraDistance = 0.75f;
        Vector3 cameraPosition = facePosition - modelForward * cameraDistance;
        cameraPosition.y = facePosition.y;
        mainCamera.transform.position = cameraPosition;
        mainCamera.transform.rotation = Quaternion.LookRotation(modelForward, Vector3.up);
        Debug.Log(string.Format(i18nMsg.VRML_CAMERA_ADJUSTED, mainCamera.transform.position, mainCamera.transform.rotation.eulerAngles));
    }

    private Transform GetHeadBone(GameObject model) {
        Animator animator = model.GetComponent<Animator>();
        if (animator != null && animator.isHuman) {
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null) return headBone;
        }
        return FindNodeByName(model.transform, new string[] { "Head", "J_Bip_C_Head" });
    }

    private Vector3 GetModelForward(GameObject model) {
        Animator animator = model.GetComponent<Animator>();
        if (animator != null && animator.isHuman) {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) return hips.forward;
        }

        // 名前マッチでヒップノードを取得（念のためフォールバック）
        Transform hipsNode = FindNodeByName(model.transform, new string[] { "Hips", "Root", "J_Bip_C_Hips" });
        return hipsNode != null ? hipsNode.forward : Vector3.forward;
        //return hipsNode != null ? hipsNode.forward : model.transform.forward; // 最後の手段として model 自身の forward
    }


    private bool IsVroidModel(GameObject model) {
        Transform hipsNode = FindNodeByName(model.transform, new string[] { "Root", "J_Bip_C_Hips" });
        Transform headNode = FindNodeByName(model.transform, new string[] { "Head", "J_Bip_C_Head" });
        return (hipsNode != null && headNode != null);
    }


    /// <summary>
    /// 子孫ノードから名前部分一致で検索（大文字小文字は無視）。
    /// 旧ロジック＋パフォーマンス微調整。
    /// </summary>
    private Transform FindNodeByName(Transform root, string[] keywords)
    {
        foreach (Transform child in root)
        {
            string name = child.name.ToLowerInvariant();
            foreach (string kw in keywords)
            {
                if (name.Contains(kw.ToLowerInvariant()))
                {
                    return child;
                }
            }
            // 再帰検索
            var hit = FindNodeByName(child, keywords);
            if (hit != null) return hit;
        }
        return null;
    }
    private Transform NEW_FindNodeByName(Transform parent, string[] nodeNames) {
        foreach (Transform child in parent) {
            foreach (string nodeName in nodeNames) {
                // ✅ 部分一致に戻す
                if (child.name.IndexOf(nodeName, StringComparison.OrdinalIgnoreCase) >= 0) {
                    return child;
                }
            }
            Transform result = FindNodeByName(child, nodeNames);
            if (result != null) return result;
        }
        return null;
    }



}
