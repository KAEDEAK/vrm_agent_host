using UniGLTF;
using UniVRM10;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class CustomVrmAnimationImporter : VrmAnimationImporter {
    public CustomVrmAnimationImporter(GltfData data)
        : base(data) {
        if (this.MaterialFactory != null) {
            var defaultMaterialParamsField = typeof(MaterialFactory).GetField("m_defaultMaterialParams", BindingFlags.NonPublic | BindingFlags.Instance);
            if (defaultMaterialParamsField != null) {
                var materialParams = (MaterialDescriptor)defaultMaterialParamsField.GetValue(this.MaterialFactory);

                if (materialParams != null) {
                    Debug.Log($"🟢 現在の Shader: {(materialParams.Shader != null ? materialParams.Shader.name : "NULL")}");
                }
                else {
                    Debug.LogError("❌ MaterialDescriptor が null です！");
                }
            }
            else {
                Debug.LogError("❌ m_defaultMaterialParams が見つかりませんでした！");
            }
        }
        else {
            Debug.LogError("❌ MaterialFactory が null です！");
        }
    }
}
