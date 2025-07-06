# WingMenuSystem トラブルシューティング・仕様書

## 概要
WingMenuSystemでメニューが見えない問題の解決方法と、必須の調整項目をまとめた仕様書です。

## 問題の症状と解決策

### 1. メニューが全く見えない問題

#### 症状
- VRM読み込み後にメニューを表示しても羽が見えない
- Sceneビューではワイヤーフレームで表示される
- 座標は正しく設定されているが視覚的に確認できない

#### 根本原因
1. **マテリアルの問題**: シェーダーがテクスチャを要求するが提供されていない
2. **カリングの問題**: メッシュが片面のみで、カメラの角度によって見えない
3. **MToonシェーダーとの競合**: VRMのMToonシェーダーとの優先度問題

#### 必須の解決策

##### A. 両面メッシュの実装
```csharp
// 頂点を表面・裏面で複製
Vector3[] vertices = new Vector3[]
{
    // 表面
    new Vector3(-0.2f, -0.6f, 0),  // 左下（根元）
    new Vector3(0.2f, -0.6f, 0),   // 右下（根元）
    new Vector3(0.3f, 0.5f, 0),    // 右上（先端）
    new Vector3(-0.3f, 0.5f, 0),   // 左上（先端）
    // 裏面（同じ頂点を複製）
    new Vector3(-0.2f, -0.6f, 0),
    new Vector3(0.2f, -0.6f, 0),
    new Vector3(0.3f, 0.5f, 0),
    new Vector3(-0.3f, 0.5f, 0)
};

// 三角形（表面と裏面の両方）
int[] triangles = new int[] { 
    // 表面（時計回り）
    0, 2, 1, 0, 3, 2,
    // 裏面（反時計回り）
    4, 5, 6, 4, 6, 7
};
```

##### B. 確実なシェーダー選択
```csharp
// 1. Unlit/Colorを最優先（テクスチャ不要）
Shader shader = Shader.Find("Unlit/Color");
if (shader != null)
{
    material = new Material(shader);
    material.color = wingColor;
    // カリングを無効にして両面表示
    if (material.HasProperty("_Cull"))
    {
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
    }
}
```

##### C. カリング無効化（必須）
```csharp
// カリングを無効にして両面表示
if (material.HasProperty("_Cull"))
{
    material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
}
```

### 2. 座標・配置の問題

#### 症状
- メニューがVRMの前面に表示される
- 背景に隠れてしまう
- カメラの向きによって見えなくなる

#### 必須の座標設定

##### A. VRM読み込み後の配置
```csharp
private void AdjustMenuSystem()
{
    if (vrmLoader != null && vrmLoader.LoadedModel != null) {
        Transform headBone = GetHeadBone(vrmLoader.LoadedModel);
        
        if (headBone != null) {
            Vector3 headPosition = headBone.position;
            
            // 【重要】Z座標を-2に固定（最適な奥行き）
            Vector3 menuPosition = new Vector3(
                headPosition.x,           // VRMのX座標
                headPosition.y - 0.3f,   // 胸の辺り
                -2.0f                    // 固定のZ座標
            );
            
            menuContainer.transform.position = menuPosition;
            menuContainer.transform.localScale = Vector3.one * wingScale;
            
            // カメラ方向を向く
            Vector3 menuForward = -mainCamera.transform.forward;
            menuContainer.transform.rotation = Quaternion.LookRotation(menuForward, Vector3.up);
        }
    }
}
```

##### B. VRM読み込み前の配置
```csharp
// VRM読み込み前：原点に配置、スケール1,1,1
menuContainer.transform.position = Vector3.zero;
menuContainer.transform.localScale = Vector3.one;
menuContainer.transform.rotation = Quaternion.identity;
```

### 3. レンダリング順序の問題

#### 必須設定
```csharp
// レンダリング順序を強制的に設定
meshRenderer.sortingOrder = 100; // 高い値で前面に表示
```

## 必須の調整項目チェックリスト

### ✅ マテリアル設定
- [ ] Unlit/Colorシェーダーを使用
- [ ] カリング無効化（_Cull = Off）
- [ ] 適切な色設定（wingColor）

### ✅ メッシュ設定
- [ ] 両面メッシュ（表面・裏面の三角形）
- [ ] 正しいUV座標設定
- [ ] RecalculateNormals()実行

### ✅ 座標設定
- [ ] VRM読み込み後：Z座標-2固定
- [ ] VRM読み込み前：原点配置
- [ ] headBone基準の位置計算

### ✅ レイヤー設定
- [ ] UIレイヤー（5）に設定
- [ ] カメラのカリングマスクに含まれている

### ✅ コライダー設定
- [ ] BoxColliderでクリック検出
- [ ] 適切なサイズ設定

## デバッグ機能

### キーボードショートカット
- **Tキー**: 通常メニューの表示/非表示切り替え
- **Uキー**: 大型テストメニュー表示（マゼンタ色、スケール2.0倍）
- **Yキー**: 詳細デバッグ情報出力

### デバッグ情報の確認項目
```csharp
private void DebugMenuVisibility() {
    // カメラ情報
    Debug.Log($"Camera Position: {mainCamera.transform.position}");
    Debug.Log($"Camera Culling Mask: {mainCamera.cullingMask}");
    
    // メニューコンテナ情報
    Debug.Log($"MenuContainer Position: {menuContainer.transform.position}");
    Debug.Log($"MenuContainer Scale: {menuContainer.transform.localScale}");
    
    // 各羽の情報
    foreach (var item in wingItems) {
        var renderer = item.wingObject.GetComponent<MeshRenderer>();
        Debug.Log($"Wing: WorldPos={item.wingObject.transform.position}, " +
                 $"Material={renderer?.material?.name}, " +
                 $"RenderQueue={renderer?.material?.renderQueue}");
    }
}
```

## 技術的な詳細

### シェーダー優先順位
1. **Unlit/Color** (最優先) - テクスチャ不要、確実に表示
2. **Standard** (フォールバック) - Opaqueモード、カリング無効
3. **Sprites/Default** (最終手段)

### 座標系の理解
- **X座標**: VRMのheadBone位置を基準
- **Y座標**: headBone - 0.3f（胸の辺り）
- **Z座標**: -2.0f固定（背景より手前、VRMより後ろ）

### カメラとの関係
- カメラが180度回転している場合を考慮
- メニューは常にカメラ方向を向く
- `menuForward = -mainCamera.transform.forward`

## よくある問題と解決法

### Q: メニューが見えない
**A**: 以下を確認
1. Unlit/Colorシェーダーが使用されているか
2. カリングが無効化されているか（_Cull = Off）
3. 両面メッシュが正しく作成されているか

### Q: 座標がずれる
**A**: 以下を確認
1. Z座標が-2.0fに固定されているか
2. headBoneが正しく取得されているか
3. VRM読み込み前後で適切な処理分岐がされているか

### Q: クリックが反応しない
**A**: 以下を確認
1. BoxColliderが設定されているか
2. UIレイヤーに設定されているか
3. カメラのカリングマスクにUIレイヤーが含まれているか

## 実装時の注意点

### 1. 必須の実装順序
1. 両面メッシュの作成
2. Unlit/Colorシェーダーの適用
3. カリング無効化
4. 座標設定（Z=-2固定）
5. レンダリング順序設定

### 2. テスト方法
1. VRM読み込み前にTキーでメニュー表示テスト
2. VRM読み込み後にTキーでメニュー表示テスト
3. Uキーで大型テストメニューの表示確認
4. Yキーでデバッグ情報の確認

### 3. パフォーマンス考慮
- メッシュは軽量（8頂点、12三角形）
- マテリアルはシンプル（Unlit/Color）
- 不要なレンダリング設定は避ける

## まとめ

WingMenuSystemの可視性問題は主に以下の3つの要因によるものでした：

1. **マテリアル問題**: テクスチャ不要のUnlit/Colorシェーダーと両面表示
2. **座標問題**: Z座標-2固定による適切な奥行き配置
3. **レンダリング問題**: カリング無効化とレンダリング順序設定

これらの調整により、MToonシェーダー使用時でも確実にメニューが表示されるようになります。
