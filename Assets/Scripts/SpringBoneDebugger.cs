using UnityEngine;
using UniVRM10;

/// <summary>
/// SpringBone動作テスト用のヘルパークラス
/// </summary>
public class SpringBoneDebugger : MonoBehaviour
{
    [Header("デバッグ設定")]
    [SerializeField] private KeyCode testKey = KeyCode.T;
    [SerializeField] private bool showSpringBoneGizmos = true;

    private AnimationHandler animHandler;
    private VRMLoader vrmLoader;

    void Start()
    {
        animHandler = FindObjectOfType<AnimationHandler>();
        vrmLoader = FindObjectOfType<VRMLoader>();
    }

    void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            TestSpringBonePreservation();
        }
    }

    void TestSpringBonePreservation()
    {
        if (vrmLoader?.VrmInstance != null)
        {
            Debug.Log("\uD83E\uDDDA SpringBone preservation test started");

            var joints = vrmLoader.VrmInstance.GetComponentsInChildren<Vrm10SpringBoneJoint>(true);
            Debug.Log($"Found {joints.Length} SpringBone joints");

            foreach (var joint in joints)
            {
                Debug.Log($"SpringBone: {joint.name} - Enabled: {joint.enabled} - Rotation: {joint.transform.localRotation}");
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!showSpringBoneGizmos || vrmLoader?.VrmInstance == null) return;

        var joints = vrmLoader.VrmInstance.GetComponentsInChildren<Vrm10SpringBoneJoint>(true);

        Gizmos.color = Color.green;
        foreach (var joint in joints)
        {
            if (joint.enabled)
            {
                Gizmos.DrawWireSphere(joint.transform.position, 0.01f);

                if (joint.transform.parent != null)
                {
                    Gizmos.DrawLine(joint.transform.position, joint.transform.parent.position);
                }
            }
        }
    }
}
